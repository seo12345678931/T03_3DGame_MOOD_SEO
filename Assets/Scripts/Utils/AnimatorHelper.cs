using UnityEngine;

namespace Mood.Utils
{
    /// <summary>
    /// Animator 파라미터 존재 여부를 확인한 뒤 안전하게 값을 설정하는 유틸리티.
    /// EnemyNavMeshController, PlayerWeaponSystem 등에서 공통으로 사용한다.
    /// </summary>
    public static class AnimatorHelper
    {
        public static bool HasParameter(Animator animator, string parameterName, AnimatorControllerParameterType parameterType)
        {
            if (animator == null || string.IsNullOrWhiteSpace(parameterName))
            {
                return false;
            }

            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.name == parameterName && parameter.type == parameterType)
                {
                    return true;
                }
            }

            return false;
        }

        public static void SetTriggerIfExists(Animator animator, string parameterName)
        {
            if (HasParameter(animator, parameterName, AnimatorControllerParameterType.Trigger))
            {
                animator.ResetTrigger(parameterName);
                animator.SetTrigger(parameterName);
            }
        }

        public static void SetBoolIfExists(Animator animator, string parameterName, bool value)
        {
            if (HasParameter(animator, parameterName, AnimatorControllerParameterType.Bool))
            {
                animator.SetBool(parameterName, value);
            }
        }

        public static void SetFloatIfExists(Animator animator, string parameterName, float value)
        {
            if (HasParameter(animator, parameterName, AnimatorControllerParameterType.Float))
            {
                animator.SetFloat(parameterName, value);
            }
        }

        public static void SetIntIfExists(Animator animator, string parameterName, int value)
        {
            if (HasParameter(animator, parameterName, AnimatorControllerParameterType.Int))
            {
                animator.SetInteger(parameterName, value);
            }
        }
    }
}
