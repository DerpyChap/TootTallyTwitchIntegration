using BaboonAPI.Hooks.Tracks;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using TootTallyCore.APIServices;
using TootTallyCore.Graphics;
using TootTallyCore.Graphics.Animations;
using TootTallyCore.Utils.Helpers;
using TootTallyCore.Utils.TootTallyNotifs;
using TrombLoader.CustomTracks;
using UnityEngine;
using UnityEngine.UI;

namespace TootTallyTwitchIntegration
{
    public static class RequestPanelManager
    {
        public static GameObject requestRowPrefab;
        public static LevelSelectController _songSelectInstance;
        public static int _songIndex;
        public static bool _isPlaying;
        private static List<RequestPanelRow> _requestRowList;
        private static List<Request> _requestList;
        private static List<BlockedRequests> _blockedList;
        private static List<int> _songIDHistory;
        public static int _currentSongID;
        public static int RequestCount => _requestList.Count;

        private static ScrollableSliderHandler _scrollableHandler;
        private static Slider _slider;

        private static RectTransform _containerRect;
        private static TootTallyAnimation _panelAnimationFG, _panelAnimationBG;

        private static GameObject _overlayPanel;
        private static GameObject _overlayCanvas;
        private static GameObject _overlayPanelContainer;
        public static bool IsPanelActive;
        private static bool _isInitialized;
        private static bool _isAnimating;
        public static void Initialize()
        {
            if (_isInitialized) return;

            _overlayCanvas = new GameObject("TwitchOverlayCanvas");
            Canvas canvas = _overlayCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1;
            CanvasScaler scaler = _overlayCanvas.AddComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _requestRowList = new List<RequestPanelRow>();
            _requestList = new List<Request>();
            _blockedList = new List<BlockedRequests>();
            _songIDHistory = new List<int>();

            GameObject.DontDestroyOnLoad(_overlayCanvas);

            _overlayPanel = GameObjectFactory.CreateOverlayPanel(_overlayCanvas.transform, Vector2.zero, new Vector2(1700, 900), 20f, "TwitchOverlayPanel");
            _overlayPanelContainer = _overlayPanel.transform.Find("FSLatencyPanel/LatencyFG/MainPage").gameObject;
            
            _slider = new GameObject("TwitchPanelSlider", typeof(Slider)).GetComponent<Slider>();
            _slider.transform.SetParent(_overlayPanel.transform);
            _slider.onValueChanged.AddListener(OnScrolling);
            _scrollableHandler = _slider.gameObject.AddComponent<ScrollableSliderHandler>();


            _overlayPanel.transform.Find("FSLatencyPanel/LatencyFG").localScale = Vector2.zero;
            _overlayPanel.transform.Find("FSLatencyPanel/LatencyBG").localScale = Vector2.zero;
            _overlayPanel.transform.Find("FSLatencyPanel/LatencyFG").GetComponent<Image>().color = new Color(.1f, .1f, .1f);
            _containerRect = _overlayPanelContainer.GetComponent<RectTransform>();
            _containerRect.anchoredPosition = new Vector2(0,-40);
            _containerRect.sizeDelta = new Vector2(1700, 900);


            var verticalLayout = _overlayPanelContainer.GetComponent<VerticalLayoutGroup>();
            verticalLayout.padding = new RectOffset(20, 20, 20, 20);
            verticalLayout.spacing = 120f;
            verticalLayout.childAlignment = TextAnchor.UpperCenter;
            verticalLayout.childControlHeight = verticalLayout.childControlWidth = true;
            _overlayPanelContainer.transform.parent.gameObject.AddComponent<Mask>();
            GameObjectFactory.DestroyFromParent(_overlayPanelContainer.transform.parent.gameObject, "subtitle");
            GameObjectFactory.DestroyFromParent(_overlayPanelContainer.transform.parent.gameObject, "title");
            var text = GameObjectFactory.CreateSingleText(_overlayPanelContainer.transform, "title", "Twitch Requests");
            text.fontSize = 60f;
            _overlayPanel.SetActive(false);
            SetRequestRowPrefab();

            _requestList = FileManager.GetRequestsFromFile();
            _requestList.ForEach(AddRowFromFile);
            _blockedList = FileManager.GetBlockedRequestsFromFile();

            IsPanelActive = false;
            _isInitialized = true;
            _isPlaying = false;
        }

        private static void OnScrolling(float value)
        {
            _containerRect.anchoredPosition = new Vector2(_containerRect.anchoredPosition.x, value * (65f * _requestList.Count) - 40f);
        }


        [HarmonyPatch(typeof(LevelSelectController), nameof(LevelSelectController.Start))]
        [HarmonyPostfix]
        public static void StartBot(LevelSelectController __instance, int ___songindex)
        {
            if (!_isInitialized) Initialize();

            _songSelectInstance = __instance;
            _songIndex = ___songindex;
            _isPlaying = false;
        }

        [HarmonyPatch(typeof(GameController), nameof(GameController.Start))]
        [HarmonyPostfix]
        public static void SetCurrentSong()
        {
            _songSelectInstance = null;
            _isPlaying = true;
            var track = TrackLookup.lookup(GlobalVariables.chosen_track_data.trackref);
            var songHash = SongDataHelper.GetSongHash(track);
            Plugin.Instance.StartCoroutine(TootTallyAPIService.GetHashInDB(songHash, track is CustomTrack, id => _currentSongID = id));
            Remove(GlobalVariables.chosen_track_data.trackref);
        }

        [HarmonyPatch(typeof(PointSceneController), nameof(PointSceneController.Start))]
        [HarmonyPostfix]
        public static void ResetCurrentSong()
        {
            _isPlaying = false;
        }

        [HarmonyPatch(typeof(LevelSelectController), nameof(LevelSelectController.advanceSongs))]
        [HarmonyPostfix]
        public static void UpdateInstance(LevelSelectController __instance, int ___songindex)
        {
            _songSelectInstance = __instance;
            _songIndex = ___songindex;
        }

        [HarmonyPatch(typeof(GameObjectFactory), nameof(GameObjectFactory.OnHomeControllerInitialize))]
        [HarmonyPostfix]
        public static void InitializeRequestPanel() => Initialize();

        [HarmonyPatch(typeof(HomeController), nameof(HomeController.Start))]
        [HarmonyPostfix]
        public static void DeInitialize()
        {
            _songSelectInstance = null;
        }

        [HarmonyPatch(typeof(LevelSelectController), nameof(LevelSelectController.clickBack))]
        [HarmonyPrefix]
        private static bool OnClickBackSkipIfPanelActive() => ShouldScrollSongs();

        [HarmonyPatch(typeof(LevelSelectController), nameof(LevelSelectController.clickNext))]
        [HarmonyPrefix]
        private static bool OnClickNextSkipIfScrollWheelUsed() => ShouldScrollSongs();

        [HarmonyPatch(typeof(LevelSelectController), nameof(LevelSelectController.clickPrev))]
        [HarmonyPrefix]
        private static bool OnClickBackSkipIfScrollWheelUsed() => ShouldScrollSongs();

        public static void TogglePanel()
        {
            if (!_isInitialized) return;

            IsPanelActive = _songSelectInstance != null && !IsPanelActive;
            _scrollableHandler.enabled = IsPanelActive && _requestRowList.Count > 6;
            _isAnimating = true;
            if (_overlayPanel != null)
            {
                _panelAnimationBG?.Dispose();
                _panelAnimationFG?.Dispose();
                var targetVector = IsPanelActive ? Vector2.one : Vector2.zero;
                var animationTime = IsPanelActive ? 1f : 0.45f;
                var secondDegreeAnimationFG = IsPanelActive ? new SecondDegreeDynamicsAnimation(1.75f, 1f, 0f) : new SecondDegreeDynamicsAnimation(3.2f, 1f, 0.25f);
                var secondDegreeAnimationBG = IsPanelActive ? new SecondDegreeDynamicsAnimation(1.75f, 1f, 0f) : new SecondDegreeDynamicsAnimation(3.2f, 1f, 0.25f);
                _panelAnimationFG = TootTallyAnimationManager.AddNewScaleAnimation(_overlayPanel.transform.Find("FSLatencyPanel/LatencyFG").gameObject, targetVector, animationTime, secondDegreeAnimationFG);
                _panelAnimationBG = TootTallyAnimationManager.AddNewScaleAnimation(_overlayPanel.transform.Find("FSLatencyPanel/LatencyBG").gameObject, targetVector, animationTime, secondDegreeAnimationBG, (sender) =>
                {
                    _isAnimating = false;
                    if (!IsPanelActive)
                        _overlayPanel.SetActive(IsPanelActive);
                });
                if (IsPanelActive)
                    _overlayPanel.SetActive(IsPanelActive);
            }
        }

        public static void AddRow(Request request)
        {
            _requestList.Add(request);
            UpdateSaveRequestFile();
            _requestRowList.Add(new RequestPanelRow(_overlayPanelContainer.transform, request));
            _scrollableHandler.accelerationMult = 6f / _requestRowList.Count;
            _scrollableHandler.enabled = _requestRowList.Count > 6;
        }

        public static void AddToBlockList(int id)
        {
            _blockedList.Add(new BlockedRequests() { songID = id });
            TootTallyNotifManager.DisplayNotif($"Song #{id} blocked.");
            FileManager.SaveBlockedRequestsToFile(_blockedList);
        }

        public static void AddRowFromFile(Request request) =>
            _requestRowList.Add(new RequestPanelRow(_overlayPanelContainer.transform, request));

        public static void Dispose()
        {
            if (!_isInitialized) return; //just in case too

            GameObject.DestroyImmediate(_overlayCanvas);
            _isInitialized = false;
        }

        public static void Remove(RequestPanelRow row)
        {
            _requestList.Remove(row.request);
            UpdateSaveRequestFile();
            _requestRowList.Remove(row);
            _slider.value = 0;
        }

        public static void Remove(string trackref)
        {
            var request = _requestRowList.Find(r => r.request.songData.track_ref == trackref);
            if (request == null) return;

            request.RemoveFromPanel();
            AddSongIDToHistory(request.request.songID);
        }

        public static void SetRequestRowPrefab()
        {
            var tempRow = GameObjectFactory.CreateOverlayPanel(_overlayCanvas.transform, Vector2.zero, new Vector2(1340, 84), 5f, $"TwitchRequestRowTemp").transform.Find("FSLatencyPanel").gameObject;
            requestRowPrefab = GameObject.Instantiate(tempRow);
            GameObject.DestroyImmediate(tempRow.gameObject);

            requestRowPrefab.name = "RequestRowPrefab";
            requestRowPrefab.transform.localScale = Vector3.one;

            requestRowPrefab.GetComponent<Image>().maskable = true;
            var container = requestRowPrefab.transform.Find("LatencyFG/MainPage").gameObject;
            container.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
            container.GetComponent<RectTransform>().sizeDelta = new Vector2(1340, 100);
            GameObject.DestroyImmediate(container.transform.parent.Find("subtitle").gameObject);
            GameObject.DestroyImmediate(container.transform.parent.Find("title").gameObject);
            GameObject.DestroyImmediate(container.GetComponent<VerticalLayoutGroup>());
            var horizontalLayoutGroup = container.AddComponent<HorizontalLayoutGroup>();
            horizontalLayoutGroup.padding = new RectOffset(20, 20, 20, 20);
            horizontalLayoutGroup.spacing = 20f;
            horizontalLayoutGroup.childAlignment = TextAnchor.MiddleLeft;
            horizontalLayoutGroup.childControlHeight = horizontalLayoutGroup.childControlWidth = false;
            horizontalLayoutGroup.childForceExpandHeight = horizontalLayoutGroup.childForceExpandWidth = false;
            requestRowPrefab.transform.Find("LatencyFG").GetComponent<Image>().maskable = true;
            requestRowPrefab.transform.Find("LatencyBG").GetComponent<Image>().maskable = true;

            GameObject.DontDestroyOnLoad(requestRowPrefab);
            requestRowPrefab.SetActive(false);
        }

        public static void SetTrackToTrackref(string trackref)
        {
            if (_songSelectInstance == null) return;
            for (int i = 0; i < _songSelectInstance.alltrackslist.Count; i++)
            {
                if (_songSelectInstance.alltrackslist[i].trackref == trackref)
                {
                    var attempts = 0;
                    while (i - _songIndex != 0 && _songSelectInstance.songindex != i && attempts <= 3)
                    {
                        // Only advance songs if we're not on the same song already
                        _songSelectInstance.advanceSongs(i - _songIndex, true);
                        attempts++;
                    }
                    return;
                }
            }
        }

        public static void AddSongIDToHistory(int id) => _songIDHistory.Add(id);
        public static string GetSongIDHistoryString() => _songIDHistory.Count > 0 ? string.Join(", ", _songIDHistory) : "No songs history recorded";

        public static string GetSongQueueIDString() => _requestList.Count > 0 ? string.Join(", ", _requestList.Select(x => x.songID)) : "No songs requested";
        public static string GetLastSongPlayed() => _songIDHistory.Count > 0 ? $"https://toottally.com/song/{_songIDHistory.Last()}" : "No song played";

        public static bool IsDuplicate(int songID) => _requestRowList.Any(x => x.request.songID == songID);

        public static bool IsBlocked(int songID) => _blockedList.Any(x => x.songID == songID);

        public static bool ShouldScrollSongs() => !IsPanelActive && !_isAnimating;

        public static void UpdateTheme()
        {
            if (!_isInitialized) return;
            Dispose();
            Initialize();
        }

        public static void UpdateSaveRequestFile()
        {
            FileManager.SaveRequestsQueueToFile(_requestList);
        }

        [Serializable]
        public class Request
        {
            public string requester;
            public SerializableClass.SongDataFromDB songData;
            public int songID;
            public string date;
        }

        [Serializable]
        public class BlockedRequests
        {
            public int songID;
        }

        public class UnprocessedRequest(string requester, int songId)
        {
            public string requester = requester;
            public int songId = songId;
        }

        public class Notif
        {
            public string message;
        }
    }
}
