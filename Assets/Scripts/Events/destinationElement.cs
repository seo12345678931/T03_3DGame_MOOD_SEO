using UnityEngine;

// 의도: 해당 오브젝트에 CompassMarker와 같이 삭제하기 위해 생성한 스크립트
namespace Mood.Events
{
    [AddComponentMenu("MOOD/Events/Destination Element")]
    [DisallowMultipleComponent]
    public sealed class DestinationElement : MonoBehaviour
    {
        public void selfDestroy()
        {
            Destroy(gameObject);
        }
    }
}
