using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using ThomassPuzzle.Enums;
using ThomassPuzzle.Helpers;
using System;
namespace ThomassPuzzle
{
    [System.Serializable]
    public class WaterRationSegment
    {
        public float Degree;
        public float x, y;
        public float heigth;
    }

    [System.Serializable]
    public class WaterRationSegments
    {
        public List<WaterRationSegment> WaterBoundList;
    }

    [System.Serializable]
    public class WaterRationBound
    {
        public List<WaterRationSegments> WaterBoundLists;

        public void SetupRect(ref float degree, LiquidObject[] L)
        {
            degree %= 360;

            int sign = 1;

            if (degree > 90)
            {
                degree -= 360;
                degree = -degree;
            }
            else
                sign = -1;


            for (int i = 0; i < WaterBoundLists.Count; i++)
            {

                WaterRationSegment start = WaterBoundLists[i].WaterBoundList[0], end = WaterBoundLists[i].WaterBoundList[2];

                foreach (var segment in WaterBoundLists[i].WaterBoundList)
                {
                    if (segment.Degree <= degree)
                        start = segment;

                    if (segment.Degree >= degree)
                    {
                        end = segment;
                        break;
                    }
                }

                var rect = L[i].GetImage().rectTransform;

                float enami = degree - start.Degree;
                float denami = end.Degree - start.Degree;
                float ratio = 0;
                if (denami > 0)
                    ratio = enami / denami;

                float newHeight = ratio * (end.heigth - start.heigth) + start.heigth;
                float newX = ratio * (end.x - start.x) + start.x;
                float newY = ratio * (end.y - start.y) + start.y;

                Vector2 pos = rect.localPosition;
                pos.x = newX * sign;
                pos.y = newY;
                rect.localPosition = pos;

                Vector2 szDelta = rect.sizeDelta;
                szDelta.y = newHeight;
                rect.sizeDelta = szDelta;

                Vector3 rot = rect.localEulerAngles;
                rot.z = degree * sign;
                rect.localEulerAngles = rot;
            }
        }
    }

    public class Flask : MonoBehaviour
    {
        #region Fields

        [SerializeField] WaterRationBound RatioBound;

        [SerializeField] LiquidObject[] LiquidObjects;

        [SerializeField] Image Content;

        [SerializeField] RectTransform RectTransform;

        [SerializeField] RectTransform DotForLiquidLine;

        [SerializeField] GameObject FinishedFlask;

        [SerializeField] public Button Button;

        private Vector2 FixedPosition;

        private FlasksSpace _parentSpace;

        private List<WaterColorEnum> _chosenColors;

        private bool _movedUp;
        private bool _inAction;
        #endregion

        #region  Methods

        public void HandleClick()
        {
            _parentSpace.SelectFlask(this);
        }
        public void ClearFlask()
        {
            for (int i = 0; i < LiquidObjects.Length; i++)
            {
                LiquidObjects[i].name = i.ToString();
                LiquidObjects[i].Clear();
                LiquidObjects[i].LastFlask = null;
            }

            if (isActiveAndEnabled)
                StartCoroutine(FlaskIsFinished(false));
        }
        public void FillFlask()
        {

            for (int i = 0; i < LiquidObjects.Length; i++)
            {
                var color = ColorsHelper.GetColor(_chosenColors[i]);
                LiquidObjects[i].Fill(color, 1);
                LiquidObjects[i].LastFlask = this;
            }

            if (isActiveAndEnabled)
                CheckFinishedFlask();
        }
        public int TopLiquidItemIndex()
        {
            for (int i = 3; i >= 0; i--)
                if (LiquidObjects[i].IsFilled())
                    return i;

            return -1;
        }
        public void MoveUp()
        {
            SetMovedUp(true);
            transform.DOMoveY(transform.position.y + .5f, 0.1f);
        }
        public void MoveDown(float delay = 0.1f)
        {
            Button.enabled = false;
            RectTransform.DOAnchorPosY(FixedPosition.y, delay).OnComplete(() =>
            {
                Button.enabled = true;
                SetMovedUp(false);
            });
        }
        public void ReturnBack(float delay = 0.1f) =>
            RectTransform.DOAnchorPos(FixedPosition, delay).OnComplete(() =>
            {
                SetMovedUp(false);
                SetInAction(false);
            });
        public void SetFlaskSpace(FlasksSpace flaskSpace) => _parentSpace = flaskSpace;
        public LiquidObject[] GetLiquidObjects() => LiquidObjects;
        public void SetChosenColors(List<WaterColorEnum> chosenColors) => _chosenColors = chosenColors;
        public void SetFixedPosition(Vector2 position) => FixedPosition = position;
        public Vector2 GetFixedPosition() => FixedPosition;
        public RectTransform GetRect() => RectTransform;
        public RectTransform GetDotRectForLiquidLine() => DotForLiquidLine;
        public Image GetContent() => Content;
        public WaterRationBound GetRatioBound() => RatioBound;
        public bool CheckFinishedFlask()
        {
            var liquidObjs = GetLiquidObjects();
            var color = liquidObjs[0].GetColorEnum();
            if (liquidObjs.Any(o => o.GetColorEnum() != color) || color == WaterColorEnum.None)
            {
                if (isActiveAndEnabled)
                    StartCoroutine(FlaskIsFinished(false));
                return false;
            }
            if (isActiveAndEnabled)
                StartCoroutine(FlaskIsFinished(true));

            return true;
        }
        public IEnumerator FlaskIsFinished(bool isFinished)
        {
            if (isFinished)
                yield return new WaitUntil(() => GetLiquidObjects().All(o => o.GetImage().fillAmount == 1));

            FinishedFlask.gameObject.SetActive(isFinished);
        }

        public void HideLiquidObjects(bool hide, int topIndex)
        {
            for (int i = topIndex; i >= 0; i--)
            {
                LiquidObjects[i].ShowLiquidObject(!hide);
            }
        }
   
        public void ShowLiquidObjectsWithSameColors()
        {
            if (!_parentSpace.IsHiddenLiquidObjects)
                return;

            var selectedTopIndex = TopLiquidItemIndex();
            if (TopLiquidItemIndex() > -1)
            {
                WaterColorEnum selectedColor = WaterColorEnum.None;
                for (int i = selectedTopIndex; i >= 0; i--)
                {
                    var liquidObject = LiquidObjects[i];
                    if (selectedColor == WaterColorEnum.None)
                        selectedColor = liquidObject.GetColorEnum();

                    if (selectedColor == liquidObject.GetColorEnum())
                    {
                        liquidObject.ShowLiquidObject(true);
                    }
                    else
                    {
                        HideLiquidObjects(true, i);
                        break;
                    }
                }

            }
            ZeroingLiquidObjectsPositions();
        }

        public bool IsMovedUp() => _movedUp;
        public void SetMovedUp(bool movedUp) => _movedUp = movedUp;
        public bool IsInAction() => _inAction;
        public void SetInAction(bool inAction) => _inAction = inAction;
        public void ZeroingLiquidObjectsPositions()
        {
            Array.ForEach(LiquidObjects, b =>
            {
                Vector2 anchoredPosition = b.GetRect().anchoredPosition;
                anchoredPosition.x = 0;
                b.GetRect().anchoredPosition = anchoredPosition;
            });
        }
        #endregion

    }
}