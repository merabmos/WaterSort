﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ThomassPuzzle.Enums;
using ThomassPuzzle.Models;
using ThomassPuzzle.Helpers;
using ThomassPuzzle.Extensions;
using System.Linq;
using System;
using ThomassPuzzle.Models.Level;
using DG.Tweening;
using UnityEngine.UI;
using ThomassPuzzle.Models.Flask;
using ThomassPuzzle.Services;
using UnityEngine.UIElements;
using Unity.VisualScripting;

namespace ThomassPuzzle
{
    public class FlasksSpace : Singleton<FlasksSpace>
    {
        #region Private Fields

        [SerializeField] Flask FlaskPrefab;

        [SerializeField] LiquidLine LiquidLinePrefab;

        [SerializeField] GridLayoutGroup GridLayout;

        [SerializeField] RectTransform rectTransform;

        [SerializeField] Canvas canvas;

        [SerializeField] RectTransform UIPanel;

        [DoNotSerialize] public List<Flask> AllFlasks;

        [DoNotSerialize] public List<LiquidLine> LiquidLines = new List<LiquidLine>();

        [DoNotSerialize] public TPGameManager GameManager;

        [DoNotSerialize] public List<Flask> SelectedFlasks = new List<Flask>();

        [DoNotSerialize] public Dictionary<int, Flask> SelectedNTarget = new Dictionary<int, Flask>() { };

        [DoNotSerialize] public Stack<SaveGamePlay> SavedGamePlays = new Stack<SaveGamePlay>();
        #endregion

        #region  Public Methods
        public void Start()
        {
            GameManager = TPGameManager.Instance;

            for (int i = 0; i < 3; i++)
            {
                var lql = Instantiate(LiquidLinePrefab, transform);
                LiquidLines.Add(lql);
            }

            Rect safeArea = Screen.safeArea;

            // Calculate the position offset based on safe area
            Vector2 offsetMin = new Vector2(safeArea.xMin / canvas.scaleFactor, safeArea.yMin / canvas.scaleFactor);
            rectTransform.offsetMin = offsetMin;

            // Apply the offset values to the RectTransform
            Vector2 offsetMax = -offsetMin;
            offsetMax.y = -UIPanel.sizeDelta.y + UIPanel.anchoredPosition.y;
            rectTransform.offsetMax = offsetMax;
        }

        public Flask CreateFlask()
        {
            GridLayout.enabled = true;
            Flask flaskObj = AllFlasks.FirstOrDefault(o => !o.isActiveAndEnabled);
            if (flaskObj)
                flaskObj.gameObject.SetActive(true);
            else
            {
                flaskObj = Instantiate(FlaskPrefab, this.transform);
                AllFlasks.Add(flaskObj);
                flaskObj.SetFlaskSpace(this);
            }

            flaskObj.transform.SetSiblingIndex(AllFlasks.IndexOf(flaskObj));

            return flaskObj;

        }
        public void CreateFlasks(Level lvl)
        {
            GridLayout.enabled = true;

            AllFlasks.ForEach(o =>
            {
                o.SetFixedPosition(default);
                o.ClearFlask();
                o.gameObject.SetActive(false);
            });

            ColorsHelper colorsHelper = new ColorsHelper();

            foreach (var flask in lvl.map)
            {
                var isFlaskEmpty = flask.values.Count > 0 ? false : true;

                Flask flaskObj = CreateFlask();

                if (!isFlaskEmpty)
                {
                    flaskObj.SetChoosedColors(colorsHelper.ColorsForFlask(flask.values));
                    flaskObj.FillFlask();
                }
                else
                    flaskObj.ClearFlask();
            }
        }
        public void SelectFlask(Flask flask)
        {

            int existsIndex = SelectedFlasks.IndexOf(flask);
            if (SelectedFlasks.Count > 0 && SelectedNTarget.ContainsValue(null))
            {
                int lastElementIndex = SelectedFlasks.Count - 1;
                if (SelectedFlasks[lastElementIndex] != flask)
                    SelectedNTarget.SetForSpecialDic(SelectedFlasks.Count - 1, flask);
                else
                {
                    FailedTry(flask);
                    return;
                }
            }
            else if (existsIndex == -1 && !SelectedNTarget.ContainsValue(flask))
            {
                if (!flask.CheckFinishedFlask())
                {
                    SaveSelectedFlask(flask);
                }
                return;
            }
            else
                return;

            OperationModel operationModel = new OperationModel(SelectedFlasks[SelectedFlasks.Count - 1], flask);

            FromFlaskToFlask(operationModel);
        }

        #endregion

        #region Private Methods
        private void SaveSelectedFlask(Flask flask)
        {
            var liquidObjects = flask.GetLiquidObjects();
            if (liquidObjects.Any(o => o.GetImage().IsActive()))
            {
                flask.SetFixedPosition(flask.GetRect().anchoredPosition);
                //flask.Button.enabled = false;

                //Check if flask is empty.
                GridLayout.enabled = false;
                SelectedFlasks.Add(flask);
                flask.transform.SetSiblingIndex(transform.childCount - 1);
                flask.MoveUp();

                SelectedNTarget.SetForSpecialDic(SelectedFlasks.Count - 1, null);
            }
        }
        private bool ConsiderColors(OperationModel operationModel)
        {
            var selectedLiquidObjects = operationModel.SelectedLiquidObjects;
            var selectedTopIndex = operationModel.SelectedTopIndex;
            var targetLiquidObjects = operationModel.TargetLiquidObjects;
            var targetTopIndex = operationModel.TargetTopIndex;

            var selectedColor = selectedLiquidObjects[selectedTopIndex].GetColorEnum();

            if (selectedTopIndex == -1)
                return false;
            if (targetTopIndex != -1)
                if (selectedColor != targetLiquidObjects[targetTopIndex].GetColorEnum())
                {
                    FailedTry(operationModel.SelectedFlask);
                    return false;
                }

            return true;
        }
        private void GainTargetLiquidObjects(OperationModel operationModel)
        {
            if (operationModel.TargetTopIndex != -1)
                FillingTargetFlask(operationModel, operationModel.TargetLiquidObjects[operationModel.TargetTopIndex].GetColorEnum());
            else
            {
                var savedColor = operationModel.SelectedLiquidObjects[operationModel.SelectedTopIndex].GetColorEnum();
                FillingTargetFlask(operationModel, savedColor);
            }
        }
        private void FromFlaskToFlask(OperationModel operationModel)
        {
            // Get all liquids from target flask

            if (operationModel.TargetTopIndex == 3 || !ConsiderColors(operationModel))
            {
                FailedTry(operationModel.SelectedFlask);
                return;
            }

            GainTargetLiquidObjects(operationModel);

            MovingToTargetFlask(operationModel);
        }
        private void MovingToTargetFlask(OperationModel operationModel)
        {
            var liquidLine = SetLiquidLine();
            
            AnimationHelper.Moving(operationModel.SelectedFlask, operationModel.TargetFlask, CalculateDelay(operationModel)).OnComplete(() =>
            {
                Rotation(operationModel,
                    operationModel.SelectedTopIndex, liquidLine);
            });
        }
        private void Rotation(OperationModel operationModel,
           int selectedTopIndex, LiquidLine liquidLine)
        {

            float endRadius = RadiusModel.GetEndRadius(selectedTopIndex);
            float midRadius = RadiusModel.GetMidRadius(selectedTopIndex);

            bool entered = false;
            AnimationHelper.Rotate(operationModel.SelectedFlask.gameObject, endRadius, .5F).OnUpdate(() =>
            {
                var crtRadius = operationModel.SelectedFlask.transform.localEulerAngles.z;
                operationModel.SelectedFlask.GetRatioBound().SetupRect(ref crtRadius, operationModel.SelectedLiquidObjects);

                if (crtRadius >= -midRadius && !entered)
                {
                    liquidLine.ShowLiquidLine(true, operationModel.TargetFlask, operationModel.SelectedLiquidObjects[selectedTopIndex].GetImage().color);
                    liquidLine.SetDeltaSize(operationModel.SelectedFlask.GetRect().anchoredPosition);
                    StartCoroutine(TransferringWithUI(operationModel,
                                                      selectedTopIndex,
                                                      liquidLine));

                    entered = true;
                }
            });
        }
        public IEnumerator TransferringWithUI(OperationModel operationModel, int selectedTopIndex, LiquidLine liquidLine)
        {
            StartCoroutine(TranslateLiquidObjects(operationModel, selectedTopIndex));
            selectedTopIndex--;

            yield return new WaitForSeconds(.5f);


            if (selectedTopIndex >= 0 && operationModel.TargetLiquidObjects.Any(o => o.GetImage().fillAmount == 0 && o.IsFilled()) &&
                 operationModel.SelectedLiquidObjects[selectedTopIndex].GetColorEnum() == operationModel.TargetLiquidObjects[operationModel.TargetFlask.TopLiquidItemIndex()].GetColorEnum())
                Rotation(operationModel, selectedTopIndex, liquidLine);
            else
            {
                LiquidObject.delay = .03f;
                var specialObjs = operationModel.TargetLiquidObjects.Where(o => o.IsFilled() && o.LastFlask == operationModel.SelectedFlask);
                yield return new WaitUntil(() => specialObjs.All(o => o.GetImage().fillAmount == 1f));
                LiquidObject.delay = 0;

                RotationBack(operationModel);
                liquidLine.ShowLiquidLine(false, null);
            }
        }
        private IEnumerator TranslateLiquidObjects(OperationModel operationModel, int selectedTopIndex)
        {
            var index = Array.FindIndex(operationModel.TargetLiquidObjects, o => o.GetImage().fillAmount == 0 &&
            o.IsFilled() &&
            o.LastFlask == operationModel.SelectedFlask);

            var from = operationModel.SelectedLiquidObjects[selectedTopIndex];
            StartCoroutine(from.MinimizeLiquid());

            if (index - 1 > -1)
                yield return new WaitUntil(() => operationModel.TargetLiquidObjects[index - 1].GetImage().fillAmount == 1f);

            var to = operationModel.TargetLiquidObjects.First(o =>
            o.GetImage().fillAmount == 0 &&
            o.IsFilled() &&
            o.LastFlask == operationModel.SelectedFlask);

            StartCoroutine(to.MaximazeLiquid());
        }
        private void FillingTargetFlask(OperationModel operationModel, WaterColorEnum comparisonColor)
        {
            var targetLqdsIndex = operationModel.TargetTopIndex;
            var liquidObjects = operationModel.SelectedLiquidObjects;
            var topLiquidItemIndex = operationModel.SelectedTopIndex;

            SaveGamePlay saveGamePlay = new SaveGamePlay()
            {
                Color = (WaterColorEnum)Enum.Parse(typeof(WaterColorEnum), liquidObjects[topLiquidItemIndex].name),
                SelectedFlask = operationModel.SelectedFlask,
                TargetFlask = operationModel.TargetFlask
            };

            bool changedSomething = false;

            while (liquidObjects[topLiquidItemIndex].GetColorEnum() == comparisonColor)
            {
                ColorModel colorModel = ColorsHelper.GetColor((WaterColorEnum)Enum.Parse(typeof(WaterColorEnum), liquidObjects[topLiquidItemIndex].name));

                targetLqdsIndex++;
                if (targetLqdsIndex > 3)
                    break;

                operationModel.TargetLiquidObjects[targetLqdsIndex].Fill(colorModel, 0);
                operationModel.TargetLiquidObjects[targetLqdsIndex].LastFlask = operationModel.SelectedFlask;
                saveGamePlay.SelectedLiquidIndex.Push(topLiquidItemIndex);
                saveGamePlay.TargetLiquidIndex.Push(targetLqdsIndex);

                changedSomething = true;
                topLiquidItemIndex--;

                //Break for avoid exception.
                if (topLiquidItemIndex < 0)
                    break;
            }
            if (changedSomething)
                SavedGamePlays.Push(saveGamePlay);
        }
        private LiquidLine SetLiquidLine()
        {
            var liquidLine = LiquidLines.Find(o => !o.IsEnabledLineImage() && o.Used == false);
            if (!liquidLine)
            {
                liquidLine = Instantiate(LiquidLinePrefab, transform);
                LiquidLines.Add(liquidLine);
            }
            else
                liquidLine.Used = true;

            liquidLine.SetFixedIndex(transform.childCount - 1);
            liquidLine.SetFixedSizeDelta(LiquidLines.First().GetFixedSizeDelta());

            return liquidLine;
        }
        private void RotationBack(OperationModel operationModel)
        {
            float delay= CalculateDelay(operationModel);
            operationModel.SelectedFlask.ReturnBack(delay);
            var liquidObjects = operationModel.SelectedFlask.GetLiquidObjects();
            AnimationHelper.Rotate(operationModel.SelectedFlask.gameObject, 0, .5f).OnUpdate(() =>
            {
                var crtRadius = operationModel.SelectedFlask.transform.localEulerAngles.z;
                operationModel.SelectedFlask.GetRatioBound().SetupRect(ref crtRadius, liquidObjects);
            }).OnComplete(() =>
            {
                EndOperation(SelectedFlasks.IndexOf(operationModel.SelectedFlask));

                StartCoroutine(FinishLevel());
            });
        }
        private IEnumerator FinishLevel()
        {
            if (!SelectedFlasks.Exists(o => o?.GetFixedPosition() != o?.GetRect().anchoredPosition) && GameManager.IsLvlDone(AllFlasks))
            {
                yield return new WaitForSeconds(.5f);
                GameManager.DoneLevel();
            }
        }
        private void EndOperation(int selectedIndex = 0)
        {
            if (selectedIndex == -1)
                return;

            var flask = SelectedFlasks[selectedIndex];

            var targetFlask = SelectedNTarget.GetForSpecialDic(selectedIndex);

            targetFlask?.CheckFinishedFlask();

            SelectedFlasks[selectedIndex] = null;
            SelectedNTarget.RemoveForSpecialDic(selectedIndex);

            if (!SelectedFlasks.Exists(o => o != null))
            {
                SelectedFlasks.Clear();
                AllFlasks.ForEach(flask =>
                {
                    if(flask.isActiveAndEnabled) { 
                        flask.transform.SetSiblingIndex(AllFlasks.IndexOf(flask));
                    }
                });
            }
        }
        private void FailedTry(Flask selectedFlask)
        {
            selectedFlask.MoveDown();
            EndOperation(SelectedFlasks.IndexOf(selectedFlask));
        }
        private float CalculateDelay(OperationModel operationModel)
        {
            var selectedFlaskIndex = AllFlasks.IndexOf(operationModel.SelectedFlask);
            var targetFlaskIndex = AllFlasks.IndexOf(operationModel.TargetFlask) + 1;
           
            int flasksCountInRow = (int)(rectTransform.rect.width / 150);

            var indexOfColumn = (targetFlaskIndex % flasksCountInRow) - 1;

            float distance = selectedFlaskIndex - indexOfColumn;
            
            distance = distance < 0 ? -distance : distance;

            float fixedDelay;
            if (distance < 5)
                fixedDelay = .3f;
            else
                fixedDelay = .5f;

            return fixedDelay;
        }

    }

    #endregion
}
