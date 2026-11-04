using UnityEngine;

public partial class NetworkManager
{
    [System.Serializable] private class TutorialState
    {
        public int revision;
        public int round;
        public string step, title, body, hint;
        public string[] cardIds;
        public bool paused, complete, inspectTable;
    }
    [System.Serializable] private class TutorialMessage { public TutorialState data; }
    [System.Serializable] private class TutorialReset { public TutorialRound data; }
    [System.Serializable] private class TutorialRound { public int round; }
    private int tutorialPreparingRound = 1;
    private bool tutorialShowingPreparation;
    private TutorialState tutorialState;
    private TutorialOverlay tutorialOverlay;
    private bool tutorialStarting;
    private bool tutorialRequested;
    private float tutorialRequestTime;
    private int tutorialWaitMessage = -1;
    private int tutorialDisplayedRevision = -1;
    private bool tutorialDisplayedPaused;
    private string tutorialDisplayedLanguage;
    private bool TutorialBlocksInput => tutorialRequested && tutorialState == null
        || tutorialStarting || (tutorialState != null && tutorialState.paused);

    private bool HandleTutorialMessage(string type, string json)
    {
        if (type == "tutorial_reset")
        {
            var reset = JsonUtility.FromJson<TutorialReset>(json);
            tutorialPreparingRound = reset.data != null ? Mathf.Max(1, reset.data.round) : 1;
            tutorialState = null;
            tutorialRequestTime = Time.realtimeSinceStartup;
            tutorialWaitMessage = -1;
            tutorialShowingPreparation = false;
            StopAllCoroutines();
            pendingTableAnimStates.Clear();
            tableAnimPipelineRunning = false;
            redealCollectRoutine = null;
            redealCollecting = false;
            ResetLocalHandState();
            if (dealAnimator != null) dealAnimator.Cancel();
            if (playAnimator != null) playAnimator.Cancel();
            if (trickWinAnimator != null) trickWinAnimator.Cancel();
            if (playChoicePopup != null) playChoicePopup.Hide();
            if (kittyView != null) kittyView.Clear();
            if (tableView != null) tableView.Clear();
            currentState = null;
            kittyPickupStarted = false;
            lastTableCardCount = 0;
            lastTrickResolveKey = lastPhaseStatus = lastElectionAnnounceKey = lastBidAnnounceKey = "";
            lastPassedCount = -1;
            choosingToastVisible = false;
            electionPresenting = false;
            playIntroUntil = passToastUntil = 0f;
            pendingPlayCardId = null;
            tutorialDisplayedRevision = -1;
            if (tutorialOverlay != null) tutorialOverlay.Hide();
            return true;
        }
        if (type == "tutorial_state")
        {
            tutorialRequested = true;
            tutorialState = JsonUtility.FromJson<TutorialMessage>(json).data;
            if (tutorialState != null && tutorialState.step == "bid")
            {
                bidScoreInput = "13";
                trumpIndex = 1;
            }
            RefreshHandPlayability();
            return true;
        }
        if (type == "tutorial_feedback")
        {
            if (tutorialOverlay != null)
                tutorialOverlay.Feedback(JsonUtility.FromJson<ErrorMsg>(json).data.message);
            return true;
        }
        return false;
    }

    private bool TutorialAllowsCard(CardData card)
    {
        return tutorialState == null || tutorialState.cardIds == null || tutorialState.cardIds.Length == 0
            || (card != null && System.Array.IndexOf(tutorialState.cardIds, card.id) >= 0);
    }

    private void ShowTutorialCardFeedback(CardData card)
    {
        if (TutorialBlocksInput || card == null || tutorialState == null) return;
        Send("{\"type\":\"tutorial_card_hint\",\"data\":{\"cardId\":\"" + card.id + "\"}}");
    }

    private void UpdateTutorial()
    {
        if (tutorialRequested && tutorialState == null)
        {
            if (tutorialOverlay == null) tutorialOverlay = gameObject.AddComponent<TutorialOverlay>();
            bool timeout = Time.realtimeSinceStartup - tutorialRequestTime > 15f;
            int message = timeout ? 1 : 0;
            if (tutorialWaitMessage == message && tutorialOverlay.IsVisible) return;
            tutorialWaitMessage = message;
            if (!timeout) ShowTutorialPreparation();
            else tutorialOverlay.Show("튜토리얼 연결을 확인해 주세요",
                "서버에서 튜토리얼 응답을 받지 못했습니다. 최신 서버를 실행한 뒤 다시 시도해 주세요.",
                "", true, false, RetryTutorial, RetryTutorial, ExitTutorial);
            return;
        }
        if (tutorialState == null) return;
        if (tutorialOverlay == null) tutorialOverlay = gameObject.AddComponent<TutorialOverlay>();
        if (IsDealInProgress())
        {
            tutorialPreparingRound = tutorialState.round;
            ShowTutorialPreparation();
            return;
        }
        if (tutorialShowingPreparation)
        {
            tutorialShowingPreparation = false;
            tutorialDisplayedRevision = -1;
            tutorialOverlay.Hide();
        }
        // Let cards settle before putting the explanation over the table.
        bool busy = IsDealInProgress() || tableAnimPipelineRunning
            || electionPresenting || Time.unscaledTime < playIntroUntil
            || (trickWinAnimator != null && trickWinAnimator.IsBusy)
            || (kittyView != null && kittyView.IsBusy);
        if (busy && tutorialState.paused) { tutorialOverlay.Hide(); return; }
        if (tutorialDisplayedRevision == tutorialState.revision
            && tutorialDisplayedPaused == tutorialState.paused
            && tutorialDisplayedLanguage == GameSettings.Language && tutorialOverlay.IsVisible) return;
        tutorialDisplayedLanguage = GameSettings.Language;
        tutorialDisplayedRevision = tutorialState.revision;
        tutorialDisplayedPaused = tutorialState.paused;
        int displayedRevision = tutorialState.revision;
        tutorialOverlay.Show(tutorialState.title, tutorialState.body, tutorialState.hint,
            tutorialState.paused, tutorialState.complete, () =>
            {
                if (tutorialState == null) return;
                Send("{\"type\":\"tutorial_ack\",\"data\":{\"revision\":" + displayedRevision + "}}");
            }, () => Send("{\"type\":\"tutorial_restart\"}"), ExitTutorial,
            StartPracticeAfterTutorial, tutorialState.inspectTable);
    }

    private void ShowTutorialPreparation()
    {
        tutorialShowingPreparation = true;
        tutorialOverlay.ShowPreparation(tutorialPreparingRound == 2
            ? "두 번째 연습 판을 준비중입니다..." : "첫 번째 연습 판을 준비중입니다...");
    }

    private void RetryTutorial()
    {
        tutorialWaitMessage = -1;
        tutorialRequestTime = Time.realtimeSinceStartup;
        Send("{\"type\":\"start_tutorial\"}");
    }

    private void ExitTutorial()
    {
        LeaveRoom();
        RoomNavigation.SetMode("");
        UnityEngine.SceneManagement.SceneManager.LoadScene(GameScenes.Title);
    }

    private void StartPracticeAfterTutorial()
    {
        LeaveRoom();
        GameScenes.StartTutorial = false;
        GameScenes.StartSinglePlayer = true;
        RoomNavigation.SetMode("singleplayer");
        UnityEngine.SceneManagement.SceneManager.LoadScene(GameScenes.Game);
    }
}
