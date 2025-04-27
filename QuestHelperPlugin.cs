using System;
using System.Collections;
using System.IO;
using System.Linq;
using BepInEx;
using UnityEngine;

namespace Erenshor_QuestHelper;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class QuestHelperPlugin : BaseUnityPlugin
{
    private Texture2D _questAvailableTexture;
    private Texture2D _questTurnInTexture;

    public const float QuestMarkerRadius = 70f;
    
    private static readonly string[] _bankNpcs = { "Prestigio Valusha", "Validus Greencent", "Comstock Retalio", "Summoned: Pocket Rift" };
    private static readonly string[] _otherNpcs = { "Thella Steepleton", "Goldie Retalio" };
    
    // Performance
    private float _questMarkerUpdateInterval = 0.5f;
    private bool _updatingQuestMarkers = false;

    private void Awake()
    {
        LoadTextures();
    }
    
    private void OnDestroy()
    {
        try
        {
            var allCharacters = GameObject.FindObjectsOfType<Character>();

            foreach (var character in allCharacters)
            {
                var marker = character.transform.Find("QuestMarkerWorldUI");
                if (marker != null)
                {
                    Destroy(marker.gameObject);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error cleaning up quest markers: {ex}");
        }
    }

    private void LoadTextures()
    {
        var assetDir = Path.Combine(Paths.PluginPath, "drizzlx-ErenshorQuestHelper");
        var questAvailablePath = Path.Combine(assetDir, "quest-available.png");
        var questTurnInPath = Path.Combine(assetDir, "quest-complete.png");
        
        if (!Directory.Exists(assetDir))
        {
            return;
        }
        
        _questAvailableTexture = LoadImage(questAvailablePath);
        _questTurnInTexture = LoadImage(questTurnInPath);
    }

    private void OnGUI()
    {
        if (GameData.PlayerControl == null || GameData.InCharSelect)
            return;

        if (GameData.Zoning)
        {
            return;
        }
        
        if (!_updatingQuestMarkers)
            StartCoroutine(UpdateQuestMarkersCoroutine());
    }

    private IEnumerator  UpdateQuestMarkersCoroutine()
    {
        _updatingQuestMarkers = true;
        
        // Get all NPC within the detection radius
        var hitColliders = Physics.OverlapSphere(GameData.PlayerControl.transform.position, QuestMarkerRadius);

        foreach (var collider in hitColliders)
        {
            if (collider == null)
                continue;
            
            var character = collider.GetComponent<Character>();

            if (character == null)
                continue;
            
            // Check if the npc has a quest to turn in.
            var questManager = character.GetComponent<QuestManager>();
            // Check if npc has potential for available quest.
            var npcDialogManager = character.GetComponent<NPCDialogManager>();

            if ((questManager == null && npcDialogManager == null) // can't be a quest giver
                || !character.isNPC // ignore non-npc
                || character.MiningNode // ignore mining nodes
                || character.transform == null 
                || character.MyNPC == null
                || character.MyNPC.SimPlayer // ignore sims
                || character.transform.name == "Player") // ignore player self
            {
                continue;
            }

            // If there is a quest manager, check for quests to turn in.
            if (questManager != null)
            {
                var showQuestTurnInMarker = ShowQuestTurnInMarker(questManager);

                if (showQuestTurnInMarker)
                {
                    AttachMarkerToCharacter(character, _questTurnInTexture);
                        
                    continue;
                }
            }
            
            // Check for available quests.
            var showQuestAvailableMarker = ShowQuestAvailableMarker(character);
                
            if (showQuestAvailableMarker)
            {
                AttachMarkerToCharacter(character, _questAvailableTexture);

                continue;
            }
            
            // Remove previous marker if quest was accepted or turned in.
            var questMarkerWorldUI = character.transform.Find("QuestMarkerWorldUI");

            if (questMarkerWorldUI != null)
            {
                Destroy(questMarkerWorldUI.gameObject);
            }

            // Wait 1 frame before continuing to the next character.
            yield return null;
        }
        
        // Wait before processing markers again.
        yield return new WaitForSeconds(_questMarkerUpdateInterval);
        
        _updatingQuestMarkers = false;
    }
    
    private void AttachMarkerToCharacter(Character character, Texture2D texture)
    {
        var questMarkerWorldUI = character.transform.Find("QuestMarkerWorldUI");
        
        // Cleanup the previous marker
        if (questMarkerWorldUI != null)
        {
            var currentMarker = questMarkerWorldUI.GetComponent<QuestMarker>();

            if (currentMarker != null)
            {
                if (currentMarker.markerType == QuestMarker.Type.Available && texture == _questAvailableTexture)
                {
                    return;
                }
            
                if (currentMarker.markerType == QuestMarker.Type.TurnIn && texture == _questTurnInTexture)
                {
                    return;
                }
            }
            
            Destroy(questMarkerWorldUI.gameObject);

            return;
        }

        var npcHeight = character.GetComponent<Collider>()?.bounds.size.y ?? 2.5f;
        npcHeight = Mathf.Clamp(npcHeight, 3.0f, 3.0f);

        var marker = CreateWorldMarker(texture);
        
        marker.name = "QuestMarkerWorldUI";
        marker.transform.SetParent(character.transform);
        marker.transform.localPosition = new Vector3(0, npcHeight + 0.1f, 0); // Above head
        marker.transform.rotation = Quaternion.identity;

        // Add billboard so it always faces the camera
        var billboard = marker.AddComponent<BillboardToCamera>();
        
        // Enable class to be called by Unity
        billboard.enabled = true;
        marker.SetActive(true);
        
        var questMarker = marker.AddComponent<QuestMarker>();

        if (texture == _questAvailableTexture)
        {
            questMarker.markerType = QuestMarker.Type.Available;
        }
        else
        {
            questMarker.markerType = QuestMarker.Type.TurnIn;
        }
    }

    private GameObject CreateWorldMarker(Texture2D texture)
    {
        // Create the base object
        var canvasGO = new GameObject("QuestMarkerWorldUI");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 0;

        var canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1f, 1f);
        canvasRect.localScale = Vector3.one * 0.02f;

        // Add the image child
        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(canvasGO.transform, false);

        var image = iconGO.AddComponent<UnityEngine.UI.RawImage>();
        image.texture = texture;
        image.raycastTarget = false;

        var imageRect = image.GetComponent<RectTransform>();
        imageRect.sizeDelta = new Vector2(64f, 64f); // Image size in world units (scaled)

        return canvasGO;
    }

    private bool ShowQuestAvailableMarker(Character character)
    {
        var npcDialogueManager = character.GetComponent<NPCDialogManager>();

        if (npcDialogueManager == null)
            return false;

        if (npcDialogueManager.SpecificClass.Count == 0 || npcDialogueManager.SpecificClass.Contains(GameData.PlayerStats.CharacterClass))
        {
            foreach (var npcDialogue in npcDialogueManager.MyDialogOptions)
            {
                var quest = npcDialogue.QuestToAssign;

                if (quest == null)
                {
                    continue;
                }
                
                if (GameData.HasQuest.Contains(quest.DBName))
                {
                    continue;
                }
            
                if (!GameData.IsQuestDone(quest.DBName))
                {
                    return true;
                }
            
                if (GameData.IsQuestDone(quest.DBName) && quest.repeatable)
                {
                    return true;
                }
            }
        }
        
        return false;
    }

    private bool ShowQuestTurnInMarker(QuestManager questManager)
    {
        foreach (var quest in questManager.NPCQuests)
        {
            if (quest == null)
                continue;
            
            if (GameData.HasQuest.Contains(quest.QuestName) 
                && (!GameData.CompletedQuests.Contains(quest.QuestName) || quest.repeatable))
            {
                var requiredItems = quest.RequiredItems;

                foreach (var requiredItem in requiredItems)
                {
                    if (requiredItem == null || GameData.PlayerInv == null)
                        continue;
                    
                    if (!GameData.PlayerInv.HasItem(requiredItem, false))
                    {
                        if (GameData.PlayerInv.mouseSlot?.MyItem?.ItemName == requiredItem.ItemName)
                        {
                            return true;
                        }

                        if (GameData.TradeWindow?.LootSlots != null)
                        {
                            foreach (var lootSlot in GameData.TradeWindow.LootSlots)
                            {
                                if (lootSlot.MyItem != null && lootSlot.MyItem.ItemName == requiredItem.ItemName)
                                {
                                    return true;
                                }
                            }
                        }
                        
                        return false;
                    }
                }
                
                return true;
            }
        }

        return false;
    }
    
    private Texture2D LoadImage(string assetPath)
    {
        if (File.Exists(assetPath))
        {
            var data = File.ReadAllBytes(assetPath);
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            
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

public class BillboardToCamera : MonoBehaviour
{
    private Camera _cam;
    private Transform _player;

    private void Start()
    {
        _cam = GameData.PlayerControl?.camera;
        _player = GameData.PlayerControl?.transform;
    }

    private void LateUpdate()
    {
        if (_cam == null)
        {
            _cam = GameData.PlayerControl?.camera;
        }

        if (_player == null)
        {
            _player = GameData.PlayerControl?.transform;
        }
        
        if (_cam == null || _player == null)
            return;

        // Rotate to face the camera
        transform.rotation = Quaternion.LookRotation(transform.position - _cam.transform.position);

        // Remove marker if it's too far away from the player
        var distance = Vector3.Distance(transform.position, _player.position);
        if (distance > QuestHelperPlugin.QuestMarkerRadius + 10f) // offset to prevent flicker
        {
            Destroy(gameObject); // Remove this marker
        }
    }
}

public class QuestMarker : MonoBehaviour
{
    public Type markerType;
    
    public enum Type
    {
        Available,
        TurnIn
    }
}