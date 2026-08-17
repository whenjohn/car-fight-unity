using CarFight.Driving;
using UnityEngine;

namespace CarFight.Presentation
{
    public sealed class CursorIntentView : MonoBehaviour
    {
        [SerializeField] private Transform cursorMarker;
        [SerializeField] private Transform maxSpeedMarker;
        [SerializeField] private Transform cursorLine;

        public void Configure(Transform marker, Transform speedMarker, Transform line)
        {
            cursorMarker = marker;
            maxSpeedMarker = speedMarker;
            cursorLine = line;
        }

        public void Render(Vector3 bodyPosition, Vector2 offset, float collisionRadius)
        {
            float roadY = bodyPosition.y - collisionRadius;
            Vector3 target = new Vector3(
                bodyPosition.x + offset.x,
                roadY + 0.04f,
                bodyPosition.z + offset.y);
            Vector3 start = new Vector3(bodyPosition.x, roadY + 0.08f, bodyPosition.z);
            float planarDistance = offset.magnitude;
            bool visible = planarDistance > 0.05f;

            if (cursorMarker != null)
            {
                cursorMarker.gameObject.SetActive(true);
                cursorMarker.position = target;
            }

            if (maxSpeedMarker != null)
            {
                maxSpeedMarker.gameObject.SetActive(visible);
                if (visible)
                {
                    Vector2 maximum = offset.normalized * FollowController.MaxDistance;
                    maxSpeedMarker.position = new Vector3(
                        bodyPosition.x + maximum.x,
                        roadY + 0.075f,
                        bodyPosition.z + maximum.y);
                }
            }

            if (cursorLine == null)
                return;

            cursorLine.gameObject.SetActive(visible);
            if (!visible)
                return;

            Vector3 direction = target - start;
            cursorLine.position = (start + target) * 0.5f;
            cursorLine.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            cursorLine.localScale = new Vector3(0.045f, 0.025f, direction.magnitude);
        }
    }
}
