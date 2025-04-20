using System.IO;
using System.Linq;
using BepInEx;
using UnityEngine;

namespace Erenshor_QuestMarkers;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class QuestMarkersPlugin : BaseUnityPlugin
{
    private Texture2D _questAvailableTexture;
    private Texture2D _questTurnInTexture;

    private float _questMarkerRadius = 100f;
       
    private void Awake()
    {
        var questAvailablePath = Path.Combine(Paths.PluginPath, "Drizzlx-Erenshor-QuestMarker", "Assets", "quest-available.png");
        var questTurnInPath = Path.Combine(Paths.PluginPath, "Drizzlx-Erenshor-QuestMarker", "Assets", "quest-complete.png");
        
        _questAvailableTexture = LoadImage(questAvailablePath);
        _questTurnInTexture = LoadImage(questTurnInPath);
    }

    private void OnGUI()
    {
        // Get all NPC within the detection radius
        Collider[] hitColliders = Physics.OverlapSphere(GameData.PlayerControl.transform.position, _questMarkerRadius);

        foreach (var collider in hitColliders)
        {
            Character character = collider.GetComponent<Character>();
                
            if (character != null 
                && character.Alive
                && character.isNPC)
            {
                var questManager = character.GetComponent<QuestManager>(); // quest receiver

                if (questManager == null)
                    continue;

                if (ShowQuestTurnInMarker(questManager))
                {
                    DrawMarkerAbove(character, _questTurnInTexture);
                    continue;
                }
                
                if (ShowQuestAvailableMarker(character))
                {
                    DrawMarkerAbove(character, _questAvailableTexture);

                }
            }
        }
    }

    private void DrawMarkerAbove(Character character, Texture2D texture)
    {
        Vector3 worldPos = character.transform.position + Vector3.up * 2.5f; // Above head
        Vector3 screenPos = GameData.PlayerControl.camera.WorldToScreenPoint(worldPos);

        if (screenPos.z > 0)
        {
            float iconSize = 64f;

            float x = screenPos.x - iconSize / 2f;
            float y = Screen.height - screenPos.y - iconSize; // Shift up by full icon height

            GUI.DrawTexture(new Rect(x, y, iconSize, iconSize), texture);
        }
    }


    private bool ShowQuestAvailableMarker(Character character)
    {
        var npcDialogueManager = character.GetComponent<NPCDialogManager>();

        if (npcDialogueManager == null)
            return false;

        foreach (var npcDialogue in npcDialogueManager.MyDialogOptions)
        {
            var completedQuest = GameData.CompletedQuests.Contains(npcDialogue.QuestToAssign.DBName);
            var quest = npcDialogue.QuestToAssign;
            
            if (!completedQuest)
            {
                return true;
            }
            
            if (quest.repeatable)
            {
                return true;
            }
        }
        
        return false;
    }

    private bool ShowQuestTurnInMarker(QuestManager questManager)
    {
        foreach (var quest in questManager.NPCQuests)
        {
            if (GameData.HasQuest.Contains(quest.QuestName) && !GameData.CompletedQuests.Contains(quest.QuestName))
            {
                return true;
            }
        }

        return false;
    }
    
    private Texture2D LoadImage(string assetPath)
    {
        if (File.Exists(assetPath))
        {
            byte[] data = File.ReadAllBytes(assetPath);
            Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(data))
            {
                Logger.LogError("Failed to load texture from " + assetPath);
            }
            return tex;
        }

        Logger.LogError("Texture not found at " + assetPath);
        return null;
    }
}
