using UnityEngine;

namespace Rasterloom.FreeIconSampler
{
    /// <summary>
    /// Walks the camera down the icon wall so the demo scene shows something
    /// the moment you press Play, then turns around at the ends.
    /// </summary>
    [DisallowMultipleComponent]
    public class IconTourCamera : MonoBehaviour
    {
        [Tooltip("World units per second.")]
        public float speed = 2.5f;

        [Tooltip("How far down the wall the tour runs before turning back.")]
        public float travel = 8f;

        private Vector3 _origin;
        private float _t;

        private void Awake()
        {
            _origin = transform.position;
        }

        private void Update()
        {
            // PingPong rather than a wrap: a hard jump back to the top reads
            // as a dropped frame, and reviewers do notice.
            _t += Time.deltaTime * speed;
            transform.position = _origin + Vector3.down * Mathf.PingPong(_t, travel);
        }
    }
}
