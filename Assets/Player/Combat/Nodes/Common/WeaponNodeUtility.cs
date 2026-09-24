using System;
using System.Threading;
using SAS.StateMachineCharacterController;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SAS.ActionGraph.WeaponSystem
{
    public abstract class WeaponActionNode<T> : ActionNode<T>
    {
        protected WeaponActionNode(ActionDataProvider<T> dataProvider) : base(dataProvider)
        {
        }

        protected static CombatActionContext RequireCombatContext(ActionContext context)
        {
            return WeaponNodeUtility.RequireCombatContext(context);
        }

        protected T GetAttackData(CombatActionContext context)
        {
            return WeaponNodeUtility.GetAttackData(_dataProvider, context);
        }
    }

    public static class WeaponNodeUtility
    {
        public static T GetAttackData<T>(ActionDataProvider<T> dataProvider, CombatActionContext context)
        {
            if (dataProvider == null)
                return default;

            int attackIndex = dataProvider.UseSingleValue || context == null ? 0 : context.CurrentAttackIndex;
            return WeaponAttackDataSelector.GetForAttack(dataProvider.GetAllData(), attackIndex);
        }

        public static CombatActionContext RequireCombatContext(ActionContext context)
        {
            var weaponContext = context as CombatActionContext;
            if (weaponContext == null)
                throw new InvalidOperationException("Weapon node requires CombatActionContext.");

            return weaponContext;
        }

        public static async Awaitable WaitForAnimationExitAsync(CombatActionContext context, int layer, string stateTag, CancellationToken token)
        {
            if (context.Animator == null)
                return;

            bool enteredState = false;
            while (true)
            {
                token.ThrowIfCancellationRequested();

                AnimatorStateInfo stateInfo = context.Animator.GetCurrentAnimatorStateInfo(layer);
                bool isInState = stateInfo.IsTag(stateTag);
                if (isInState)
                    enteredState = true;
                else if (enteredState)
                    return;

                await Awaitable.NextFrameAsync(token);
            }
        }

        public static Vector3 GetOwnerForward(CombatActionContext context)
        {
            if (context.Owner != null)
            {
                ICharacter character = context.Owner.GetComponentInParent<ICharacter>();
                if (character != null)
                    return character.Forward;
            }

            Transform origin = context.OriginTransform;
            return origin != null ? origin.forward : Vector3.forward;
        }

        public static IMovementVectorHandler GetMovementVectorHandler(CombatActionContext context)
        {
            if (context.Owner == null)
                return null;

            MonoBehaviour[] behaviours = context.Owner.GetComponentsInParent<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null)
                    continue;

                if (behaviour is IMovementVectorHandler movementVectorHandler)
                    return movementVectorHandler;
            }

            return null;
        }

        public static IMovementVelocityComposer GetMovementVelocityComposer(CombatActionContext context)
        {
            if (context.Owner == null)
                return null;

            MonoBehaviour[] behaviours = context.Owner.GetComponentsInParent<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null)
                    continue;

                if (behaviour is IMovementVelocityComposer movementVelocityComposer)
                    return movementVelocityComposer;
            }

            return null;
        }

        public static Vector3 GetMovementForward(CombatActionContext context, Component movementComponent)
        {
            if (movementComponent != null)
                return movementComponent.transform.forward;

            return GetOwnerForward(context);
        }

        public static Component GetMovementTargetComponent(IMovementVelocityComposer movementVelocityComposer,
            IMovementVectorHandler movementVectorHandler)
        {
            if (movementVelocityComposer is Component composerComponent)
                return composerComponent;

            if (movementVectorHandler is Component handlerComponent)
                return handlerComponent;

            return null;
        }

        public static bool TrySetMovementVector(CombatActionContext context, Vector3 movement)
        {
            IMovementVectorHandler movementVectorHandler = GetMovementVectorHandler(context);
            if (movementVectorHandler == null)
                return false;

            movementVectorHandler.MovementVector = movement;
            return true;
        }

        public static GameObject AttachChild(GameObject prefab, Transform root, string socketPath)
        {
            Transform socket = FindByFullPath(root, socketPath);
            if (prefab == null || socket == null)
                return null;

            GameObject instance = Object.Instantiate(prefab, socket, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            return instance;
        }

        private static Transform FindByFullPath(Transform root, string fullPath)
        {
            if (root == null || string.IsNullOrEmpty(fullPath))
                return null;

            if (fullPath.StartsWith(root.name + "/", StringComparison.Ordinal))
                fullPath = fullPath.Substring(root.name.Length + 1);

            Transform socket = root.Find(fullPath);
            if (socket == null)
                Debug.LogWarning($"[WeaponAttachModels] Socket not found: {fullPath} under {root.name}");

            return socket;
        }
    }
}
