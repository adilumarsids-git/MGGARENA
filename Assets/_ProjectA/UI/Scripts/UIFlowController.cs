using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectA.UI
{
    public class UIFlowController : MonoBehaviour
    {
        [Header("Scene Names")]
        [SerializeField] private string bootSceneName = "Boot";
        [SerializeField] private string titleSceneName = "Title";
        [SerializeField] private string lobbySceneName = "Lobby";
        [SerializeField] private string matchSceneName = "Match";
        [SerializeField] private string resultsSceneName = "Results";

        [Header("Panels")]
        [SerializeField] private BootPanel bootPanel;
        [SerializeField] private TitlePanel titlePanel;
        [SerializeField] private LobbyPanel lobbyPanel;
        [SerializeField] private QueuePanel queuePanel;
        [SerializeField] private HUDPanel hudPanel;
        [SerializeField] private ResultsPanel resultsPanel;
        [SerializeField] private ErrorModal errorModal;

        private readonly Dictionary<AppState, ProjectAPanel> _statePanels = new();

        public AppState CurrentState { get; private set; } = AppState.Boot;

        private void Awake()
        {
            RegisterPanels();
            SetState(AppState.Boot, false);
        }

        public void SetState(AppState nextState, bool switchScene = true)
        {
            CurrentState = nextState;
            HideAllPanels();

            if (_statePanels.TryGetValue(nextState, out var panel) && panel != null)
            {
                panel.SetVisible(true);
            }

            if (switchScene)
            {
                var sceneName = ResolveSceneName(nextState);
                if (!string.IsNullOrWhiteSpace(sceneName) && SceneManager.GetActiveScene().name != sceneName)
                {
                    SceneManager.LoadScene(sceneName);
                }
            }
        }

        public void ShowErrorModal(bool isVisible)
        {
            if (errorModal != null)
            {
                errorModal.SetVisible(isVisible);
            }
        }

        private void RegisterPanels()
        {
            _statePanels.Clear();
            _statePanels[AppState.Boot] = bootPanel;
            _statePanels[AppState.Title] = titlePanel;
            _statePanels[AppState.Lobby] = lobbyPanel;
            _statePanels[AppState.Queue] = queuePanel;
            _statePanels[AppState.Match] = hudPanel;
            _statePanels[AppState.Results] = resultsPanel;
        }

        private void HideAllPanels()
        {
            foreach (var panel in _statePanels.Values)
            {
                panel?.SetVisible(false);
            }

            errorModal?.SetVisible(false);
        }

        private string ResolveSceneName(AppState state)
        {
            return state switch
            {
                AppState.Boot => bootSceneName,
                AppState.Title => titleSceneName,
                AppState.Lobby => lobbySceneName,
                AppState.Queue => lobbySceneName,
                AppState.Match => matchSceneName,
                AppState.Results => resultsSceneName,
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
            };
        }
    }
}
