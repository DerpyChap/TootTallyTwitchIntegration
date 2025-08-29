using System;
using TootTallyAccounts;
using TootTallyTwitchLibs;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using UnityEngine;
using static TootTallyTwitchIntegration.RequestPanelManager;

namespace TootTallyTwitchIntegration
{
    public class TwitchRequestController : MonoBehaviour
    {
        private TwitchLibsController _controller;
        private RequestPanelController _requestPannelController;

        public void Awake()
        {
            gameObject.TryGetComponent(out TwitchLibsController controller);
            if (controller == null)
            {
                Plugin.LogError("TwitchLibsController couldn't be found.");
                return;
            }
            _controller = controller;
            _controller.OnChatCommandReceived += ClientHandleChatCommand;
            _requestPannelController = gameObject.AddComponent<RequestPanelController>();
        }

        public void OnDestroy()
        {
            _controller.OnChatCommandReceived -= ClientHandleChatCommand;
            RequestPanelManager.Dispose();
        }

        private void ClientHandleChatCommand(TwitchLibsController controller, OnChatCommandReceivedArgs args)
        {
            var command = args.Command;
            switch (Enum.Parse(typeof(Commands), command.CommandText))
            {
                case Commands.ttr:
                    if (Plugin.Instance.EnableRequestsCommand.Value)
                        OnTTRCommand(controller, command);
                    break;
                case Commands.profile:
                    if (Plugin.Instance.EnableProfileCommand.Value && TootTallyUser.userInfo.id > 0)
                        controller.SendChannelMessage($"!TootTally Profile: https://toottally.com/profile/{TootTallyUser.userInfo.id}");
                    break;
                case "song": // Get current song
                    if (Plugin.Instance.EnableCurrentSongCommand.Value && RequestPanelManager._currentSongID != 0)
                        controller.SendChannelMessage($"!Current Song: https://toottally.com/song/{RequestPanelManager._currentSongID}");
                    break;
                case "ttrhelp":
                    if (Plugin.Instance.EnableCurrentSongCommand.Value)
                        controller.SendChannelMessage($"!Use !ttr to request a chart use its TootTally Song ID! To get a song ID, search for the song in https://toottally.com/search/ (Example: !ttr 3781)");
                    break;
                case "queue":
                    if (Plugin.Instance.EnableCurrentSongCommand.Value)
                        controller.SendChannelMessage($"!Song Queue: {RequestPanelManager.GetSongQueueIDString()}");
                    break;
                case "last":
                    if (Plugin.Instance.EnableCurrentSongCommand.Value)
                        controller.SendChannelMessage($"!Last song played: {RequestPanelManager.GetLastSongPlayed()}");
                    break;
                case "history":
                    if (Plugin.Instance.EnableCurrentSongCommand.Value)
                        controller.SendChannelMessage($"!Songs played: {RequestPanelManager.GetSongIDHistoryString()}");
                    break;
                default:
                    break;
            }
        }

        private void OnTTRCommand(TwitchLibsController controller, ChatCommand command)
        {
            var args = command.ArgumentsAsList;
            if (args.Count == 0)
            {
                controller.SendChannelMessage($"!Use !ttr to request a chart use its TootTally Song ID! To get a song ID, search for the song in https://toottally.com/search/ (Example: !ttr 3781)");
                return;
            }

            if (int.TryParse(args[0], out int song_id))
            {
                Plugin.LogInfo($"Successfully parsed request for {song_id}, submitting to stack.");
                RequestSong(song_id, command.ChatMessage);
            }
            else
            {
                Plugin.LogInfo("Could not parse request input, ignoring.");
                controller.SendChannelMessage("!Invalid song ID. Please try again.");
            }
        }

        public void RequestSong(int songId, ChatMessage message)
        {
            //TODO Add BlacklistUser Check
            if (Plugin.Instance.SubOnlyMode.Value && !message.IsSubscriber) return;


            if (RequestPanelManager.IsBlocked(songId))
            {
                _controller.SendChannelMessage($"!Song #{songId} is blocked.");
                return;
            }
            else if (RequestPanelManager.IsDuplicate(songId) || _requestPannelController.IsDuplicate(songId))
            {
                _controller.SendChannelMessage($"!Song #{songId} already requested.");
                return;
            }
            else if (RequestPanelManager.RequestCount >= Plugin.Instance.MaxRequestCount.Value)
            {
                _controller.SendChannelMessage($"!Request cap reached.");
                return;
            }
            UnprocessedRequest request = new UnprocessedRequest(message.Username, songId);
            Plugin.LogInfo($"Accepted request {songId} by {message.Username}.");
            _controller.SendChannelMessage($"!Song #{songId} successfully requested.");
            _requestPannelController.QueueRequest(request);
        }

        public enum Commands
        {
            ttr,
            profile,
            song,
            ttrhelp,
            queue,
            last,
            history
        }


    }
}
