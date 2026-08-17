using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Rewired;
using Rewired.Data;
namespace InputFramework
{
    [HarmonyPatch(typeof(InputManager_Base), "Awake")]
    public static class RewiredActionInjector
    {
    	private static void Prefix(InputManager_Base __instance)
    	{
    		InjectActions(__instance);
    	}
    	private static void InjectActions(InputManager_Base manager)
    	{
    		UserData userData = manager._userData;
    		if (userData == null)
    		{
    			return;
    		}
    		List<InputAction> actions = userData.actions;
    		if (actions == null)
    		{
    			return;
    		}
    		List<InputActionCategory> actionCategories = userData.actionCategories;
    		if (actionCategories == null)
    		{
    			return;
    		}
    		InputActionCategory val = actionCategories.FirstOrDefault((InputActionCategory c) => ((InputCategory)c).name == "Debug");
    		int nextActionId = GetNextActionId(actions);
    		foreach (ModActionDefinition modAction in ExtraInputManager.PendingActions)
    		{
    			if (actions.Any((InputAction a) => a.name == modAction.Name))
    			{
    				continue;
    			}
    			InputActionCategory val2 = val;
    			if (modAction.Category != null)
    			{
    				val2 = actionCategories.FirstOrDefault((InputActionCategory c) => ((InputCategory)c).name == modAction.Category);
    			}
    			InputAction val3 = new InputAction();
    			val3.id = nextActionId++;
    			val3.name = modAction.Name;
    			val3.type = modAction.Type;
    			val3.descriptiveName = modAction.Name;
    			val3.categoryId = ((InputCategory)val2).id;
    			InputAction val4 = val3;
    			val4._userAssignable = true;
    			actions.Add(val4);
    			userData.actionCategoryMap.AddAction(((InputCategory)val2).id, val4.id);
    			modAction.AssignedId = val4.id;
    		}
    		ExtraInputManager.RewiredInitialized = true;
    	}
    	private static int GetNextActionId(List<InputAction> actions)
    	{
    		if (actions.Count == 0)
    		{
    			return 1000;
    		}
    		return actions.Max((InputAction a) => a.id) + 1;
    	}
    }
}