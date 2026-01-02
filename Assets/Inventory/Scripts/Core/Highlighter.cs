using UnityEngine;
using UnityEngine.UI;

namespace Inventory.Scripts.Core
{
    public class Highlighter : MonoBehaviour
    {
        private RectTransform _rectTransform;
        private Image _image;

        private bool _initialized;

        private void Awake()
        {
            Init();
        }

        private void Init()
        {
            if (_initialized) return;

            _rectTransform = GetComponent<RectTransform>();
            _image = GetComponent<Image>();

            transform.localScale = Vector3.one;

            _initialized = true;
        }

        public void Show(bool shouldShow)
        {
            Init();

            gameObject.SetActive(shouldShow);
        }

        public void SetColor(Color color)
        {
            Init();

            _image.color = color;
        }

        public void SetParent(RectTransform highlightParent)
        {
            Init();

            _rectTransform.SetParent(highlightParent);
        }

        public void SetLocalPosition(Vector2 localPositionOnGrid)
        {
            Init();

            _rectTransform.localPosition = localPositionOnGrid;
        }

        public void SetSizeDelta(Vector2 size)
        {
            Init();

            _rectTransform.sizeDelta = size;
        }
    }
}