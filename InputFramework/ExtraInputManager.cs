using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Rewired;
using UnityEngine;
namespace InputFramework
{
    public static class ExtraInputManager
    {
    	private static readonly List<ModActionDefinition> pendingActions = new List<ModActionDefinition>();
    	private static readonly string savePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "ExtraInputActions.json");
    	internal static bool RewiredInitialized { get; set; } = false;
    	public static IReadOnlyList<ModActionDefinition> PendingActions => pendingActions;
    	public static void RegisterAction(string name, InputActionType type, string category = null)
    	{
    		if (string.IsNullOrEmpty(name))
    		{
    			throw new ArgumentException("Action name cannot be null or empty.");
    		}
    		if (pendingActions.Any((ModActionDefinition a) => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase) && a.Type == type && string.Equals(a.Category ?? "", category ?? "", StringComparison.OrdinalIgnoreCase)))
    		{
    			Debug.Log((object)string.Format("[ExtraInputManager] Action \"{0}\" (Type={1}, Category={2}) has been Registed, skipping.", name, type, category ?? "null"));
    			return;
    		}
    		if (RewiredInitialized)
    		{
    			Debug.LogWarning((object)("[ExtraInputManager] Registering Action \"" + name + "\" after Rewired Awake. You need to restart the game to apply the changes"));
    		}
    		ModActionDefinition item = new ModActionDefinition(name, type, category);
    		pendingActions.Add(item);
    		SavePendingActions();
    	}
    	private static void SavePendingActions()
    	{
    		try
    		{
    			string contents = JsonConvert.SerializeObject(pendingActions, Formatting.Indented);
    			File.WriteAllText(savePath, contents);
    		}
    		catch (Exception arg)
    		{
    			Debug.LogError((object)$"[ExtraInputManager] Savint Register of Action failed: {arg}");
    		}
    	}
    	public static void LoadPendingActions()
    	{
    		try
    		{
    			if (File.Exists(savePath))
    			{
    				string value = File.ReadAllText(savePath);
    				List<ModActionDefinition> list = JsonConvert.DeserializeObject<List<ModActionDefinition>>(value);
    				if (list != null)
    				{
    					pendingActions.Clear();
    					pendingActions.AddRange(list);
    				}
    			}
    		}
    		catch (Exception arg)
    		{
    			Debug.LogError((object)$"[ExtraInputManager] Reading registered Action list failed: {arg}");
    		}
    	}
    }
}