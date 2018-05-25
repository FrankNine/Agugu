﻿using UnityEngine;
using UnityEngine.UI;


public class BuildUguiGameObjectVisitor : IUiNodeVisitor
{
    private readonly Rect _parentRect;
    private readonly RectTransform _parent;

    public BuildUguiGameObjectVisitor(Rect parentRect, RectTransform parent)
    {
        _parentRect = parentRect;
        _parent = parent;
    }

    public GameObject Visit(UiTreeRoot root)
    {
        var uiRootGameObject = new GameObject(root.Name);

        var uiRootRectTransform = uiRootGameObject.AddComponent<RectTransform>();
        uiRootRectTransform.anchorMin = Vector2.zero;
        uiRootRectTransform.anchorMax = Vector2.one;
        uiRootRectTransform.offsetMin = Vector2.zero;
        uiRootRectTransform.offsetMax = Vector2.zero;
        uiRootRectTransform.ForceUpdateRectTransforms();
        

        var baseRect = new Rect(0, 0, root.Width, root.Height);
        var childrenVisitor = new BuildUguiGameObjectVisitor(baseRect, uiRootRectTransform);
        root.Children.ForEach(child => child.Accept(childrenVisitor));

        return uiRootGameObject;
    }

    public void Visit(GroupNode node)
    {
        if (node.IsSkipped) { return; }

        var groupGameObject = new GameObject(node.Name);
        var groupRectTransform = groupGameObject.AddComponent<RectTransform>();
        groupGameObject.transform.SetParent(_parent, worldPositionStays: false);

        var layerIdTag = groupGameObject.AddComponent<PsdLayerIdTag>();
        layerIdTag.LayerId = node.Id;

        _SetRectTransform
        (
            groupRectTransform,
            node.Rect, _parentRect,
            _GetAnchorMin(node), _GetAnchorMax(node),
            node.Pivot
        );

        if (node.HasScrollRect)
        {
            var scrollRect = groupGameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = node.IsScrollRectHorizontal;
            scrollRect.vertical = node.IsScrollRectVertical;

            string containerGameObjectName = node.Name + "Container";
            var containerGameObject = new GameObject(containerGameObjectName);
            var containerRectTransform = containerGameObject.AddComponent<RectTransform>();
            containerGameObject.transform.SetParent(groupRectTransform, worldPositionStays: false);

            scrollRect.content = containerRectTransform;

            groupGameObject.AddComponent<Image>();

            var mask = groupGameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            _SetRectTransform
            (
                containerRectTransform,
                node.Rect, node.Rect,
                _GetAnchorMin(node), _GetAnchorMax(node),
                new Vector2(0.5f, 0.5f)
            );

            if (node.HasGrid)
            {
                var gridLayoutGroup = containerGameObject.AddComponent<GridLayoutGroup>();
                gridLayoutGroup.cellSize = node.CellSize;
                gridLayoutGroup.spacing = node.Spacing;

                var contentSizeFitter = containerGameObject.AddComponent<ContentSizeFitter>();
                if (node.IsScrollRectHorizontal)
                {
                    contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.MinSize;
                }

                if (node.IsScrollRectVertical)
                {
                    contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.MinSize;
                }
            }

            var childrenVisitor = new BuildUguiGameObjectVisitor(node.Rect, containerRectTransform);
            node.Children.ForEach(child => child.Accept(childrenVisitor));
        }
        else
        {
            var childrenVisitor = new BuildUguiGameObjectVisitor(node.Rect, groupRectTransform);
            node.Children.ForEach(child => child.Accept(childrenVisitor));
        }

        groupGameObject.SetActive(node.IsVisible);
        
    }

    public void Visit(TextNode node)
    {
        if (node.IsSkipped) { return; }

        var uiGameObject = new GameObject(node.Name);
        var uiRectTransform = uiGameObject.AddComponent<RectTransform>();

        var layerIdTag = uiGameObject.AddComponent<PsdLayerIdTag>();
        layerIdTag.LayerId = node.Id;

        var text = uiGameObject.AddComponent<Text>();
        text.text = node.Text;
        text.color = node.TextColor;
        text.font = AguguConfig.Instance.GetFont(node.FontName);
        // TODO: Wild guess, cannot find any reference about Unity font size
        // 25/6
        text.fontSize = (int)(node.FontSize / 4.16);
        text.resizeTextForBestFit = true;

        _SetRectTransform
        (
            uiRectTransform,
            node.Rect, _parentRect,
            _GetAnchorMin(node), _GetAnchorMax(node),
            node.Pivot
        );

        uiGameObject.transform.SetParent(_parent, worldPositionStays: false);
        uiGameObject.SetActive(node.IsVisible);
    }

    public void Visit(ImageNode node)
    {
        if (node.IsSkipped) { return; }

        var importedSprite = node.SpriteSource.GetSprite();

        var uiGameObject = new GameObject(node.Name);
        var uiRectTransform = uiGameObject.AddComponent<RectTransform>();
        var image = uiGameObject.AddComponent<Image>();
        image.sprite = importedSprite;

        var layerIdTag = uiGameObject.AddComponent<PsdLayerIdTag>();
        layerIdTag.LayerId = node.Id;

        _SetRectTransform
        (
            uiRectTransform,
            node.Rect, _parentRect,
            _GetAnchorMin(node), _GetAnchorMax(node),
            node.Pivot
        );

        // Have to set localPosition before parenting
        // Or the last imported layer will be reset to 0, 0, 0, I think it's a bug :(
        uiGameObject.transform.SetParent(_parent, worldPositionStays: false);

        if (node.WidgetType == WidgetType.Button)
        {
            uiGameObject.AddComponent<Button>();
        }
        uiGameObject.SetActive(node.IsVisible);
    }

    private Vector2 _GetAnchorMin(UiNode node)
    {
        float x;
        switch (node.XAnchor)
        {
            case XAnchorType.Left:    x = 0;    break;
            case XAnchorType.Center:  x = 0.5f; break;
            case XAnchorType.Right:   x = 1;    break;
            case XAnchorType.Stretch: x = 0;    break;
            default:                  x = 0.5f; break;
        }

        float y;
        switch (node.YAnchor)
        {
            case YAnchorType.Bottom:  y = 0;    break;
            case YAnchorType.Middle:  y = 0.5f; break;
            case YAnchorType.Top:     y = 1;    break;
            case YAnchorType.Stretch: y = 0;    break;
            default:                  y = 0.5f; break;
        }

        return new Vector2(x, y);
    }

    private Vector2 _GetAnchorMax(UiNode node)
    {
        float x;
        switch (node.XAnchor)
        {
            case XAnchorType.Left:    x = 0;    break;
            case XAnchorType.Center:  x = 0.5f; break;
            case XAnchorType.Right:   x = 1;    break;
            case XAnchorType.Stretch: x = 1;    break;
            default:                  x = 0.5f; break;
        }

        float y;
        switch (node.YAnchor)
        {
            case YAnchorType.Bottom:  y = 0;    break;
            case YAnchorType.Middle:  y = 0.5f; break;
            case YAnchorType.Top:     y = 1;    break;
            case YAnchorType.Stretch: y = 1;    break;
            default:                  y = 0.5f; break;
        }

        return new Vector2(x, y);
    }

    private static void _SetRectTransform
    (
        RectTransform rectTransform,
        Rect rect, Rect parentRect,
        Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot
    )
    {
        Vector2 anchorMinPosition = Vector2Extension.LerpUnclamped(parentRect.min, parentRect.max, anchorMin);
        Vector2 anchorMaxPosition = Vector2Extension.LerpUnclamped(parentRect.min, parentRect.max, anchorMax);
        Vector2 anchorSize = anchorMaxPosition - anchorMinPosition;
        Vector2 anchorReferencePosition = Vector2Extension.LerpUnclamped(anchorMinPosition, anchorMaxPosition, pivot);
        Vector2 pivotPosition = Vector2Extension.LerpUnclamped(rect.min, rect.max, pivot);

        rectTransform.pivot = pivot;
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.anchoredPosition = pivotPosition - anchorReferencePosition;
        rectTransform.sizeDelta = new Vector2(rect.width, rect.height) - anchorSize;
    }
}