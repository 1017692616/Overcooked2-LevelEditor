using LevelEditorStub;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System;
using UnityEngine;


namespace LevelEditor
{
	public class PseudoPrefabCookingUtensil : PseudoPrefab
	{
        public override void Setup()
        {
            PseudoPrefabCookingUtensilStub ingredientContainerStub = (PseudoPrefabCookingUtensilStub)stub;
            IngredientContainer ingredientContainer = childGameObject.GetComponent<IngredientContainer>();
            if (ingredientContainer != null && ingredientContainerStub.capacity > 0)
                ingredientContainer.m_capacity = ingredientContainerStub.capacity;
            if (ingredientContainerStub.allowedIngredientSOs != null &&
                ingredientContainerStub.allowedIngredientSOs.Length > 0)
            {
                SetupCookableContainer(ingredientContainerStub.allowedIngredientSOs);
                SetupMixableContainer(ingredientContainerStub.allowedIngredientSOs);
            }

            var contentsCosmeticDecisions = childGameObject.RequestComponentRecursive<ContentsCosmeticDecisions>();
            if (contentsCosmeticDecisions != null)
            {
                contentsCosmeticDecisions.m_contentsYPositionWhenEmpty = -0.2f;
            }
        }

        private void SetupCookableContainer(PseudoPrefabSO[] allowedIngredientSOs)
        {
            CookableContainer cookableContainer = childGameObject.GetComponent<CookableContainer>();
            if (cookableContainer == null || cookableContainer.m_approvedContentsList == null)
                return;

            OrderToPrefabLookup oldLookup = cookableContainer.m_approvedContentsList;
            OrderToPrefabLookup newLookup = ScriptableObject.Instantiate(oldLookup);
            FieldInfo lookupField = oldLookup.GetType().GetField(
                "m_lookupArray", BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic);
            var oldLookupArray = (OrderToPrefabLookup.ContentPrefabLookup[])lookupField.GetValue(oldLookup);
            if (oldLookupArray == null || oldLookupArray.Length == 0)
                return;

            GameObject defaultPrefab = oldLookupArray[0].m_prefab;
            OrderToPrefabLookup.ContentPrefabLookup[] allowedIngredients = allowedIngredientSOs
                .Select(RecipeHelper.GetIngredientOrderNode)
                .Where(x => x != null)
                .SelectMany(ingredientOrderNode =>
                {
                    var allLookup = oldLookupArray.Where(y =>
                    {
                        if (y.m_content == ingredientOrderNode) return true;
                        if (y.m_content is CookedCompositeOrderNode)
                        {
                            CookedCompositeOrderNode cooked = (CookedCompositeOrderNode)y.m_content;
                            return cooked.m_composition.Length == 1 &&
                                cooked.m_composition[0] == ingredientOrderNode;
                        }
                        return false;
                    });
                    return allLookup.Any()
                        ? allLookup
                        : new[] { new OrderToPrefabLookup.ContentPrefabLookup
                        {
                            m_content = ingredientOrderNode,
                            m_prefab = defaultPrefab,
                        } };
                }).ToArray();
            lookupField.SetValue(newLookup, allowedIngredients);
            cookableContainer.m_approvedContentsList = newLookup;
        }

        private void SetupMixableContainer(PseudoPrefabSO[] allowedIngredientSOs)
        {
            Component mixableContainer = childGameObject.GetComponent("MixableContainer");
            if (mixableContainer == null)
                return;

            FieldInfo approvedField = mixableContainer.GetType().GetField(
                "m_ApprovedIngredients",
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (approvedField == null || !approvedField.FieldType.IsArray ||
                approvedField.FieldType.GetElementType() == null)
                return;

            Type elementType = approvedField.FieldType.GetElementType();
            IngredientOrderNode[] nodes = allowedIngredientSOs
                .Select(RecipeHelper.GetIngredientOrderNode)
                .Where(x => x != null)
                .ToArray();
            Array approvedIngredients = Array.CreateInstance(elementType, nodes.Length);
            for (int i = 0; i < nodes.Length; i++)
            {
                if (!elementType.IsInstanceOfType(nodes[i]))
                    return;
                approvedIngredients.SetValue(nodes[i], i);
            }
            approvedField.SetValue(mixableContainer, approvedIngredients);
        }
    }
}
