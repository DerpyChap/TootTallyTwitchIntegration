using System;
using System.Collections.Concurrent;
using System.Linq;
using TootTallyCore.APIServices;
using TootTallyCore.Utils.TootTallyNotifs;
using UnityEngine;
using static TootTallyTwitchIntegration.RequestPanelManager;

namespace TootTallyTwitchIntegration
{
    public class RequestPanelController : MonoBehaviour
    {
        private ConcurrentQueue<Notif> NotifQueue;
        private ConcurrentQueue<UnprocessedRequest> RequestQueue; // Unfinished request stack, only song ids here

        public void Awake()
        {
            NotifQueue = new ConcurrentQueue<Notif>();
            RequestQueue = new ConcurrentQueue<UnprocessedRequest>();
        }

        public void OnDestroy()
        {
            NotifQueue?.Clear();
            NotifQueue = null;
            RequestQueue?.Clear();
            RequestQueue = null;
        }

        public void Update()
        {
            if (RequestPanelManager._isPlaying) return;

            if (Input.GetKeyDown(Plugin.Instance.ToggleRequestPanel.Value))
                RequestPanelManager.TogglePanel();

            if (Input.GetKeyDown(KeyCode.Escape) && RequestPanelManager.IsPanelActive)
                RequestPanelManager.TogglePanel();

            if (RequestQueue.TryDequeue(out UnprocessedRequest request))
            {
                Plugin.LogInfo($"Attempting to get song data for ID {request.songId}");
                Plugin.Instance.StartCoroutine(TootTallyAPIService.GetSongDataFromDB(request.songId, (songdata) =>
                {
                    Plugin.LogInfo($"Obtained request by {request.requester} for song {songdata.author} - {songdata.name}");
                    TootTallyNotifManager.DisplayNotif($"Requested song by {request.requester}: {songdata.author} - {songdata.name}");
                    var processed_request = new Request
                    {
                        requester = request.requester,
                        songData = songdata,
                        songID = request.songId,
                        date = DateTime.Now.ToString()
                    };
                    RequestPanelManager.AddRow(processed_request);
                }));
            }

            if (NotifQueue.TryDequeue(out Notif notif))
            {
                Plugin.LogInfo("Attempting to generate notification...");
                TootTallyNotifManager.DisplayNotif(notif.message);
            }
        }

        public void QueueRequest(UnprocessedRequest request) => RequestQueue.Enqueue(request);

        public bool IsDuplicate(int songId) => RequestQueue.Any(s => s.songId == songId);

    }
}
