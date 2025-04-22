using System;
using System.IO;
using BepInEx;
using UnityEngine;

namespace Erenshor_QuestMarkers;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class QuestMarkersPlugin : BaseUnityPlugin
{
    private Texture2D _questAvailableTexture;
    private Texture2D _questTurnInTexture;

    public const float QuestMarkerRadius = 70f;

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
        var assetDir = Path.Combine(Paths.PluginPath, "drizzlx-ErenshorQuestMarkers");
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
        
        if (_questAvailableTexture == null || _questTurnInTexture == null)
        {
            LoadTextures();
        }
        
        // Get all NPC within the detection radius
        Collider[] hitColliders = Physics.OverlapSphere(GameData.PlayerControl.transform.position, QuestMarkerRadius);

        foreach (var collider in hitColliders)
        {
            Character character = collider.GetComponent<Character>();

            if (character == null || !character.isNPC)
            {
                continue;
            }
            
            // Remove markers after quest state change.
            var marker = character.transform.Find("QuestMarkerWorldUI");
            var rawImage = marker?.GetComponentInChildren<UnityEngine.UI.RawImage>();
            
            // Check if the npc has a quest to turn in.
            var questManager = character.GetComponent<QuestManager>();

            if (questManager != null)
            {
                var showQuestTurnInMarker = ShowQuestTurnInMarker(questManager);
                
                // Remove outdated marker
                if (!showQuestTurnInMarker && rawImage != null && rawImage.texture == _questTurnInTexture)
                {
                    Destroy(marker.gameObject);

                    continue;
                }

                if (showQuestTurnInMarker)
                {
                    AttachMarkerToCharacter(character, _questTurnInTexture);
                        
                    continue;
                }
            }
                
            var showQuestAvailableMarker = ShowQuestAvailableMarker(character);
            
            // Remove outdated marker
            if (!showQuestAvailableMarker && rawImage != null && rawImage.texture == _questAvailableTexture)
            {
                Destroy(marker.gameObject);

                continue;
            }
                
            if (showQuestAvailableMarker)
            {
                AttachMarkerToCharacter(character, _questAvailableTexture);
            }
        }
    }
    
    private void AttachMarkerToCharacter(Character character, Texture2D texture)
    {
        // Prevent duplicates
        if (character.transform.Find("QuestMarkerWorldUI") != null)
            return;

        float npcHeight = character.GetComponent<Collider>()?.bounds.size.y ?? 2.5f;
        npcHeight = Mathf.Clamp(npcHeight, 3.0f, 3.0f);

        var marker = CreateWorldMarker(texture);
        
        marker.name = "QuestMarkerWorldUI";
        marker.transform.SetParent(character.transform);
        marker.transform.localPosition = new Vector3(0, npcHeight + 0.2f, 0); // Above head
        marker.transform.rotation = Quaternion.identity;

        // Add billboard so it always faces the camera
        var billboard = marker.AddComponent<BillboardToCamera>();
        
        // Enable class to be called by Unity
        billboard.enabled = true;
        marker.SetActive(true);
    }

    private GameObject CreateWorldMarker(Texture2D texture)
    {
        // Create the base object
        GameObject canvasGO = new GameObject("QuestMarkerWorldUI");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 0;

        var canvasRect = canvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1f, 1f);
        canvasRect.localScale = Vector3.one * 0.02f;

        // Add the image child
        GameObject iconGO = new GameObject("Icon");
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
        float distance = Vector3.Distance(transform.position, _player.position);
        if (distance > QuestMarkersPlugin.QuestMarkerRadius)
        {
            Destroy(gameObject); // Remove this marker
        }
    }
}
