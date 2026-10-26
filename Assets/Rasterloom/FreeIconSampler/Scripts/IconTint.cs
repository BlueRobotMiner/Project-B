using UnityEngine;

namespace Rasterloom.FreeIconSampler
{
    /// <summary>
    /// Cycles the tint of every SpriteRenderer below this object.
    /// </summary>
    /// <remarks>
    /// This is here to make one point that a screenshot cannot: the white
    /// icons are white so that you never need a second copy of an icon in a
    /// second colour. Every icon in this pack is a pure white shape with a
    /// transparent background, so a tint - Image.color in UI, or the
    /// SpriteRenderer colour here - gives you the icon in any colour your
    /// interface needs, including the disabled grey and the danger red, with
    /// no extra texture and no extra draw call.
    ///
    /// The black colourway is the same shapes for light interfaces, where
    /// tinting white down to dark grey would leave you fighting the alpha.
    /// </remarks>
    [DisallowMultipleComponent]
    public class IconTint : MonoBehaviour
    {
        [Tooltip("Seconds spent on each colour.")]
        public float hold = 1.6f;

        public Color[] colours = new Color[]
        {
            new Color(0.94f, 0.95f, 0.97f),   // default UI white
            new Color(1.00f, 0.77f, 0.24f),   // gold, for currency and rewards
            new Color(0.42f, 0.82f, 1.00f),   // cyan, for mana and info
            new Color(0.98f, 0.36f, 0.36f),   // red, for damage and danger
            new Color(0.45f, 0.48f, 0.54f)    // grey, for disabled controls
        };

        private SpriteRenderer[] _targets;
        private float _t;
        private int _i;

        private void Awake()
        {
            // Cached once. GetComponentsInChildren over 520 renderers every
            // frame would be the slowest thing in the scene by a wide margin.
            _targets = GetComponentsInChildren<SpriteRenderer>(true);
            Apply();
        }

        private void Update()
        {
            if (colours == null || colours.Length == 0)
            {
                return;
            }

            _t += Time.deltaTime;
            if (_t < hold)
            {
                return;
            }

            _t = 0f;
            _i = (_i + 1) % colours.Length;
            Apply();
        }

        private void Apply()
        {
            if (_targets == null || colours == null || colours.Length == 0)
            {
                return;
            }

            Color c = colours[_i % colours.Length];
            for (int i = 0; i < _targets.Length; i++)
            {
                if (_targets[i] != null)
                {
                    _targets[i].color = c;
                }
            }
        }
    }
}
