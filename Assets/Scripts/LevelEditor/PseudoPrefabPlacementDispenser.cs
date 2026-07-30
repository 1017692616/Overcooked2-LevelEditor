using System;
using System.Linq;
using System.Reflection;
using LevelEditorStub;
using UnityEngine;


namespace LevelEditor
{
    public class PseudoPrefabPlacementDispenser : PseudoPrefab
    {
        public override void Setup()
        {
            PseudoPrefabPlacementDispenserStub dispenserStub =
                (PseudoPrefabPlacementDispenserStub)stub;
            if (dispenserStub.ingredientSOs == null || dispenserStub.ingredientSOs.Length == 0)
                return;

            Component switcher = childGameObject.GetComponent("PlacementItemSwitcher");
            if (switcher == null)
                return;

            FieldInfo ingredientsField = switcher.GetType().GetField(
                "m_ingredients",
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (ingredientsField == null || !ingredientsField.FieldType.IsArray ||
                ingredientsField.FieldType.GetElementType() == null)
                return;

            Type elementType = ingredientsField.FieldType.GetElementType();
            var nodes = dispenserStub.ingredientSOs
                .Select(RecipeHelper.GetIngredientOrderNode)
                .Where(x => x != null)
                .ToArray();
            Array ingredients = Array.CreateInstance(elementType, nodes.Length);
            for (int i = 0; i < nodes.Length; i++)
            {
                if (!elementType.IsInstanceOfType(nodes[i]))
                    return;
                ingredients.SetValue(nodes[i], i);
            }
            ingredientsField.SetValue(switcher, ingredients);
        }
    }
}
