using Rewired;
namespace InputFramework
{
    public class ModActionDefinition
    {
    	public string Name;
    	public InputActionType Type;
    	public string Category;
    	public int AssignedId = -1;
    	public ModActionDefinition(string name, InputActionType type, string category = null)
    	{
    		Name = name;
    		Type = type;
    		Category = category;
    	}
    }
}