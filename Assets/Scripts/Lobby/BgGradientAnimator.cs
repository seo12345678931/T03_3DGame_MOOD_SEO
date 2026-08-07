using UnityEngine;
using UnityEngine.UI;

namespace Mood.Lobby
{
    [AddComponentMenu("MOOD/Lobby/Bg Gradient Animator")]
    [DisallowMultipleComponent]
    public class BgGradientAnimator : MonoBehaviour
    {
        [SerializeField] private Image targetImage;
        [SerializeField] private Gradient gradient;
        [SerializeField] private float duration = 2f;

        private void Update()
        {
            // 시간에 따라 그라디언트를 0~1사이 왕복하는 연출
            float T = Mathf.PingPong(Time.time / duration, 1f);
            targetImage.color = gradient.Evaluate(T);
        }
    }
}
