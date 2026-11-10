using System.Collections.Generic;
using PrimeTween;
using UnityEngine;

namespace BrotatoLike
{
    /// <summary>Matériau lâché par les ennemis : attiré par le joueur proche, donne matériaux + XP.</summary>
    public class Pickup : MonoBehaviour
    {
        public static readonly List<Pickup> Active = new();

        // Combo de ramassage : le son monte quand on enchaîne les matériaux
        static int pickupCombo;
        static float lastPickupTime = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCombo()
        {
            pickupCombo = 0;
            lastPickupTime = -1f;
        }

        static void PlayPickupSound(bool crate)
        {
            if (crate)
            {
                AudioManager.Play(SoundId.PickupCrate);
                return;
            }
            var juice = JuiceSettings.Settings;
            pickupCombo = lastPickupTime >= 0f && Time.time - lastPickupTime < juice.pickupComboWindow ? pickupCombo + 1 : 0;
            lastPickupTime = Time.time;
            AudioManager.Play(SoundId.PickupMaterial, 1f, Mathf.Min(1f + pickupCombo * juice.pickupPitchStep, juice.pickupPitchMax));
        }

        [SerializeField] SpriteRenderer spriteRenderer;

        int value;
        bool isCrate;
        Player target;
        float speed;
        Vector2 position;
        Vector2 popVelocity;
        int activeIndex = -1;

        // Réseau
        static ushort nextNetId;
        public ushort NetId { get; private set; }
        /// <summary>Index du sprite (0-2 = matériaux, 3 = caisse) pour les clients.</summary>
        public byte NetSprite { get; private set; }
        public bool IsReplica { get; private set; }
        Vector2 replicaTarget;

        public void Spawn(int amount, Sprite sprite, Vector2 popImpulse, bool crate = false, byte netSprite = 0)
        {
            IsReplica = false;
            NetId = ++nextNetId == 0 ? ++nextNetId : nextNetId;
            NetSprite = netSprite;
            value = amount;
            isCrate = crate;
            spriteRenderer.sprite = sprite;
            // Petit « pop » à l'apparition
            var size = Vector3.one * (crate ? 1f : 0.7f);
            Tween.StopAll(transform);
            float pop = JuiceSettings.Settings.pickupPopTime;
            if (pop > 0f)
            {
                transform.localScale = Vector3.zero;
                Tween.Scale(transform, size, pop, Ease.OutBack);
            }
            else transform.localScale = size;
            target = null;
            speed = 0f;
            position = transform.position;
            popVelocity = popImpulse;

            activeIndex = Active.Count;
            Active.Add(this);
        }

        /// <summary>Client en ligne : copie d'affichage d'un matériau de l'hôte.</summary>
        public void SpawnReplica(ushort netId, byte netSprite, Sprite sprite, Vector2 position)
        {
            IsReplica = true;
            NetId = netId;
            NetSprite = netSprite;
            isCrate = netSprite == 3;
            transform.localScale = Vector3.one * (isCrate ? 1f : 0.7f);
            spriteRenderer.sprite = sprite;
            this.position = replicaTarget = position;
            transform.position = position;
            activeIndex = Active.Count;
            Active.Add(this);
        }

        public void ApplySnapshot(Vector2 position) => replicaTarget = position;

        /// <summary>Disparition d'une réplique : petit son si elle a été ramassée par le joueur de ce PC.</summary>
        public void DespawnReplica()
        {
            var local = Player.LocalOnline;
            if (local != null && (local.Position - position).sqrMagnitude < 1.5f * 1.5f)
                PlayPickupSound(isCrate);
            Despawn();
        }

        public void Tick(float dt)
        {
            if (IsReplica)
            {
                position = Vector2.Lerp(position, replicaTarget, 1f - Mathf.Exp(-18f * dt));
                transform.position = position;
                spriteRenderer.sortingOrder = SortingUtil.OrderFromY(position.y) - 1;
                return;
            }

            position += popVelocity * dt;
            popVelocity *= Mathf.Exp(-8f * dt);

            if (target != null && !target.IsAlive) target = null;
            if (target == null) FindTarget();

            if (target != null)
            {
                speed += 30f * dt;
                position = Vector2.MoveTowards(position, target.Position, speed * dt);
                if ((target.Position - position).sqrMagnitude < 0.25f * 0.25f)
                {
                    if (isCrate) target.CollectCrate();
                    else target.CollectMaterials(value);
                    PlayPickupSound(isCrate);
                    Despawn();
                    return;
                }
            }

            transform.position = position;
            spriteRenderer.sortingOrder = SortingUtil.OrderFromY(position.y) - 1;
        }

        void FindTarget()
        {
            foreach (var p in Player.All)
            {
                if (!p.IsAlive) continue;
                float range = p.Stats[StatType.PickupRange];
                if ((p.Position - position).sqrMagnitude <= range * range)
                {
                    target = p;
                    speed = 3f;
                    return;
                }
            }
        }

        /// <summary>Fin de vague : le matériau vole directement vers ce joueur.</summary>
        public void FlyTo(Player player)
        {
            target = player;
            speed = 12f;
        }

        public void Despawn()
        {
            if (activeIndex < 0) return;
            int last = Active.Count - 1;
            var moved = Active[last];
            Active[activeIndex] = moved;
            moved.activeIndex = activeIndex;
            Active.RemoveAt(last);
            activeIndex = -1;
            Tween.StopAll(transform);

            GameManager.Instance.PickupPool.Release(this);
        }
    }
}
