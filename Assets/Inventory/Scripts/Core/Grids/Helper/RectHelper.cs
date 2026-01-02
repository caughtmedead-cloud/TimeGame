using System;
using Inventory.Scripts.Core.Helper;
using Inventory.Scripts.Core.Items.Helper;
using TMPro;
using UnityEngine;

namespace Inventory.Scripts.Core.Grids.Helper
{
    public enum AnchorPosition
    {
        TopRight,
        TopLeft,
        BottomRight,
        BottomLeft,
        MiddleRight,
        MiddleLeft,
        MiddleTop,
        MiddleBottom,
        Center
    }

    public static class RectHelper
    {
        public static Vector2 GetSizeDeltaFittingChildren(Transform transform)
        {
            var sizeFittingAllChildren = Vector2.zero;

            // Iterate through each child of the parent
            foreach (Transform child in transform)
            {
                var childRectTransform = child.GetComponent<RectTransform>();

                if (childRectTransform == null) continue;

                sizeFittingAllChildren.x = Mathf.Max(sizeFittingAllChildren.x,
                    childRectTransform.anchoredPosition.x + childRectTransform.sizeDelta.x);
                sizeFittingAllChildren.y = Mathf.Max(sizeFittingAllChildren.y,
                    -childRectTransform.anchoredPosition.y + childRectTransform.sizeDelta.y);
            }

            return sizeFittingAllChildren;
        }

        public static void SetLeftTopParentEdge(RectTransform rectTransform)
        {
            SetLeftTopParentEdge(rectTransform, rectTransform);
        }

        public static void SetLeftTopParentEdge(RectTransform rectTransform, RectTransform parentRectTransform)
        {
            rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, 0, parentRectTransform.rect.width);
            rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top, 0, parentRectTransform.rect.height);
        }

        public static void SetAnchorsForGrid(RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                Debug.LogError("rectTransform is null... Could not update the anchors".Configuration());
                return;
            }

            var imageAnchoredPosition = new Vector2(0, 1);

            rectTransform.anchoredPosition = imageAnchoredPosition;
            rectTransform.anchorMin = imageAnchoredPosition;
            rectTransform.anchorMax = imageAnchoredPosition;
            rectTransform.pivot = imageAnchoredPosition;
        }

        public static Color ChangeAlphaColor(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }

        public static void SetDisplayFillerProperties(RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                Debug.LogError("rectTransform is null... Could not set display filler properties".Configuration());
                return;
            }

            var zero = new Vector2(0, 0);

            rectTransform.anchoredPosition = zero;
            rectTransform.anchorMin = zero;
            rectTransform.anchorMax = zero;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }

        public static void SetOptionsControllerProperties(RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                Debug.LogError("rectTransform is null... Could not set display filler properties".Configuration());
                return;
            }

            var anchors = new Vector2(0, 1);

            rectTransform.anchoredPosition = anchors;
            rectTransform.anchorMin = anchors;
            rectTransform.anchorMax = anchors;
            rectTransform.pivot = anchors;
        }

        public static void SetOptionButtonProperties(RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                Debug.LogError("rectTransform is null... Could not set display filler properties".Configuration());
                return;
            }

            var zero = new Vector2(0, 0);

            rectTransform.anchoredPosition = zero;
            rectTransform.anchorMin = zero;
            rectTransform.anchorMax = zero;
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
        }

        public static void SetOptionButtonTextProperties(RectTransform rectTransform)
        {
            if (rectTransform == null)
            {
                Debug.LogError("rectTransform is null... Could not set display filler properties".Configuration());
                return;
            }

            rectTransform.anchoredPosition = new Vector2(0f, 0f);
            rectTransform.anchorMin = new Vector2(0f, 0f);
            rectTransform.anchorMax = new Vector2(1f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = new Vector2(0f, 0f);
        }

        public static void RotateText(TMP_Text text, AnchorPosition anchorPosition, bool itemIsRotated,
            Rotation rotationType = Rotation.MinusNinety)
        {
            Vector3 anchorMax;

            if (!text.TryGetComponent(out RectTransform textRT)) return;

            Vector2 anchorMin = anchorPosition switch
            {
                AnchorPosition.TopRight => anchorMax = new Vector2(1, 1),
                AnchorPosition.TopLeft => anchorMax = new Vector2(0, 1),
                AnchorPosition.BottomRight => anchorMax = new Vector2(1, 0),
                AnchorPosition.BottomLeft => anchorMax = new Vector2(0, 0),
                AnchorPosition.MiddleRight => anchorMax = new Vector2(1, 0.5f),
                AnchorPosition.MiddleLeft => anchorMax = new Vector2(0, 0.5f),
                AnchorPosition.MiddleTop => anchorMax = new Vector2(0.5f, 1),
                AnchorPosition.MiddleBottom => anchorMax = new Vector2(0.5f, 0),
                AnchorPosition.Center => anchorMax = new Vector2(0.5f, 0.5f),
                _ => throw new ArgumentOutOfRangeException(nameof(anchorPosition), anchorPosition, null)
            };

            if (rotationType == Rotation.MinusNinety)
            {
                textRT.rotation = Quaternion.Euler(0, 0, itemIsRotated ? -0f : 0f);
            }

            textRT.anchoredPosition = Vector2.zero;
            textRT.anchorMin = anchorMin;
            textRT.anchorMax = anchorMax;
        }

        public static void ResizeText(TMP_Text text, bool itemIsRotated, RectTransform rectTransform)
        {
            if (!text.TryGetComponent(out RectTransform textRT)) return;

            textRT.sizeDelta = GetSizeDeltaForItemText(itemIsRotated, rectTransform);
        }

        private static Vector2 GetSizeDeltaForItemText(bool itemRotated, RectTransform rectTransform)
        {
            var sizeDelta = rectTransform.sizeDelta;

            return itemRotated ? new Vector2(sizeDelta.y, sizeDelta.x) : sizeDelta;
        }

        /// <summary>
        /// Will adjust the Anchor based if the item is rotated or not. 
        /// </summary>
        /// <param name="anchor">The not rotated anchor</param>
        /// <param name="itemIsRotated">bool if the item is rotated</param>
        /// <returns>The normalized anchor for the rotation, if itemIsRotated is true</returns>
        /// <exception cref="ArgumentOutOfRangeException">If mismatch anchor type.</exception>
        public static AnchorPosition RotatedAnchor(AnchorPosition anchor, bool itemIsRotated)
        {
            // TODO: Fix this code for PlusNinety rotationType. Today the MinusNinety is working fine if this code.
            // The idea is to add a if rotationType == PlusNinety, and put the switch for the PlusNinety case here.
            switch (anchor)
            {
                case AnchorPosition.TopRight:
                    return itemIsRotated ? AnchorPosition.TopLeft : AnchorPosition.TopRight;
                case AnchorPosition.TopLeft:
                    return itemIsRotated ? AnchorPosition.BottomLeft : AnchorPosition.TopLeft;
                case AnchorPosition.BottomRight:
                    return itemIsRotated ? AnchorPosition.TopRight : AnchorPosition.BottomRight;
                case AnchorPosition.BottomLeft:
                    return itemIsRotated ? AnchorPosition.BottomRight : AnchorPosition.BottomLeft;
                case AnchorPosition.MiddleRight:
                    return itemIsRotated ? AnchorPosition.MiddleTop : AnchorPosition.MiddleRight;
                case AnchorPosition.MiddleLeft:
                    return itemIsRotated ? AnchorPosition.MiddleBottom : AnchorPosition.MiddleLeft;
                case AnchorPosition.MiddleTop:
                    return itemIsRotated ? AnchorPosition.MiddleLeft : AnchorPosition.MiddleTop;
                case AnchorPosition.MiddleBottom:
                    return itemIsRotated ? AnchorPosition.MiddleRight : AnchorPosition.MiddleBottom;
                case AnchorPosition.Center:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(anchor), anchor, null);
            }

            return anchor;
        }
    }
}