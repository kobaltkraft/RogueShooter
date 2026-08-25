using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using RogueArena.Core;

namespace RogueArena.UI
{
    /// <summary>Pause overlay: resume, restart, settings quick-sliders, quit to menu.</summary>
    public class PauseScreen : MonoBehaviour
    {
        GameObject root;

        public static PauseScreen Build(UiRoot owner)
        {
            var screen = owner.gameObject.AddComponent<PauseScreen>();
            screen.Create(owner.Canvas.transform);
            return screen;
        }

        void Create(Transform canvas)
        {
            root = new GameObject("PauseScreen").AddComponent<RectTransform>().gameObject;
            root.transform.SetParent(canvas, false);
            var rect = (RectTransform)root.transform;
            UiKit.Stretch(rect);
            root.SetActive(false);

            // Dim backdrop.
            var dim = UiKit.Fill(root.transform, "Dim", new Color(0f, 0f, 0f, .68f));
            UiKit.Stretch(dim.rectTransform);

            var panel = UiKit.Panel(root.transform, "Panel", UiKit.PanelMid);
            panel.rectTransform.anchorMin = panel.rectTransform.anchorMax = new Vector2(.5f, .5f);
            panel.rectTransform.sizeDelta = new Vector2(520f, 620f);

            var title = UiKit.Label(panel.transform, "Title", "PAUSED", 44, UiKit.Accent,
                TextAnchor.UpperCenter, FontStyle.Bold);
            UiKit.Stretch(title.rectTransform, top: 28f);
            title.rectTransform.sizeDelta = new Vector2(0f, 60f);

            // Buttons.
            AddButton(panel.transform, "RESUME", () => ownerResume(), 0);
            AddButton(panel.transform, "RESTART RUN", () =>
            {
                Hide();
                Services.Flow.RestartRun();
            }, 1);
            AddButton(panel.transform, "QUIT TO MENU", () =>
            {
                Hide();
                Services.Flow.QuitToMenu();
            }, 2);

            // Quick volume sliders.
            AddSliderRow(panel.transform, "MASTER", 480f, "Master");
            AddSliderRow(panel.transform, "MUSIC", 520f, "Music");
            AddSliderRow(panel.transform, "SFX", 560f, "Sfx");

            var hint = UiKit.Label(panel.transform, "Hint", "ESC to resume", 16, UiKit.TextDim, TextAnchor.LowerCenter);
            UiKit.Stretch(hint.rectTransform, bottom: 12f);
            hint.rectTransform.sizeDelta = new Vector2(0f, 22f);

            void ownerResume()
            {
                UiRoot ownerRoot = GetComponentInParent<UiRoot>() ?? FindAnyObjectByType<UiRoot>();
                ownerRoot?.Resume();
            }
        }

        void AddButton(Transform parent, string label, System.Action action, int index)
        {
            var button = UiKit.Button(parent, "Btn_" + label, label, 24, action);
            button.transform.SetParent(parent, false);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -130f - index * 74f);
            rect.sizeDelta = new Vector2(380f, 58f);
        }

        void AddSliderRow(Transform parent, string label, float y, string category)
        {
            var row = new GameObject("Row_" + label).AddComponent<RectTransform>();
            row.transform.SetParent(parent, false);
            row.anchorMin = row.anchorMax = new Vector2(.5f, 1f);
            row.anchoredPosition = new Vector2(0f, -y);
            row.sizeDelta = new Vector2(420f, 34f);

            var text = UiKit.Label(row, "Label", label, 18, UiKit.TextDim, TextAnchor.MiddleLeft);
            text.rectTransform.anchorMin = new Vector2(0f, 0f);
            text.rectTransform.anchorMax = new Vector2(.3f, 1f);

            var slider = UiKit.Slider(row, "Slider", 1f, v =>
            {
                if (Services.Settings == null) return;
                var parsed = System.Enum.TryParse<Audio.AudioCategory>(category, out var cat) ? cat : Audio.AudioCategory.Sfx;
                Services.Settings.SetVolume(cat, v);
            });
            slider.transform.SetParent(row, false);
            var sliderRect = (RectTransform)slider.transform;
            sliderRect.anchorMin = new Vector2(.35f, .5f);
            sliderRect.anchorMax = new Vector2(1f, .5f);
            sliderRect.sizeDelta = new Vector2(0f, 24f);
        }

        public void Show()
        {
            root.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Hide() => root.SetActive(false);
    }

    /// <summary>Victory / defeat overlays with run stats.</summary>
    public class EndScreens : MonoBehaviour
    {
        GameObject victoryRoot;
        GameObject defeatRoot;

        public static EndScreens Build(UiRoot owner)
        {
            var screen = owner.gameObject.AddComponent<EndScreens>();
            screen.Create(owner.Canvas.transform);
            return screen;
        }

        void Create(Transform canvas)
        {
            victoryRoot = BuildEndScreen(canvas, "VICTORY", new Color(.3f, 1f, .5f), "RUN COMPLETE");
            defeatRoot = BuildEndScreen(canvas, "DEFEAT", new Color(1f, .32f, .25f), "YOU DIED");
            victoryRoot.SetActive(false);
            defeatRoot.SetActive(false);

            GameEvents.Victory += OnVictory;
            GameEvents.Defeat += OnDefeat;
        }

        void OnDestroy()
        {
            GameEvents.Victory -= OnVictory;
            GameEvents.Defeat -= OnDefeat;
        }

        GameObject BuildEndScreen(Transform canvas, string title, Color color, string subtitle)
        {
            var root = new GameObject("End_" + title).AddComponent<RectTransform>().gameObject;
            root.transform.SetParent(canvas, false);
            UiKit.Stretch((RectTransform)root.transform);

            var dim = UiKit.Fill(root.transform, "Dim", new Color(0f, 0f, 0f, .78f));
            UiKit.Stretch(dim.rectTransform);

            var panel = UiKit.Panel(root.transform, "Panel", UiKit.PanelMid);
            panel.rectTransform.anchorMin = panel.rectTransform.anchorMax = new Vector2(.5f, .5f);
            panel.rectTransform.sizeDelta = new Vector2(560f, 460f);

            var titleText = UiKit.Label(panel.transform, "Title", title, 54, color,
                TextAnchor.UpperCenter, FontStyle.Bold);
            UiKit.Stretch(titleText.rectTransform, top: 30f);
            titleText.rectTransform.sizeDelta = new Vector2(0f, 70f);

            var sub = UiKit.Label(panel.transform, "Sub", subtitle, 20, UiKit.TextDim, TextAnchor.UpperCenter);
            UiKit.Stretch(sub.rectTransform, top: 96f);
            sub.rectTransform.sizeDelta = new Vector2(0f, 26f);

            var stats = UiKit.Label(panel.transform, "Stats", "", 24, UiKit.TextMain, TextAnchor.UpperCenter);
            UiKit.Stretch(stats.rectTransform, top: 160f);
            stats.rectTransform.sizeDelta = new Vector2(0f, 120f);
            stats.name = "Stats";

            // Buttons.
            var restart = UiKit.Button(panel.transform, "Restart", "RUN IT AGAIN", 24, () =>
            {
                HideAll();
                Services.Flow.RestartRun();
            });
            restart.transform.SetParent(panel.transform, false);
            var restartRect = (RectTransform)restart.transform;
            restartRect.anchorMin = restartRect.anchorMax = new Vector2(.5f, 0f);
            restartRect.anchoredPosition = new Vector2(-110f, 30f);
            restartRect.sizeDelta = new Vector2(200f, 56f);

            var quit = UiKit.Button(panel.transform, "Quit", "MAIN MENU", 24, () =>
            {
                HideAll();
                Services.Flow.QuitToMenu();
            });
            quit.transform.SetParent(panel.transform, false);
            var quitRect = (RectTransform)quit.transform;
            quitRect.anchorMin = quitRect.anchorMax = new Vector2(.5f, 0f);
            quitRect.anchoredPosition = new Vector2(110f, 30f);
            quitRect.sizeDelta = new Vector2(200f, 56f);

            return root;
        }

        void OnVictory() => Show(victoryRoot, "VICTORY");
        void OnDefeat() => Show(defeatRoot, "DEFEAT");

        void Show(GameObject screenRoot, string kind)
        {
            StartCoroutine(RevealAfter(screenRoot, kind == "VICTORY" ? 1.6f : 2.2f));
        }

        IEnumerator RevealAfter(GameObject screenRoot, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);

            screenRoot.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            var stats = screenRoot.transform.Find("Panel")?.Find("Stats")?.GetComponent<Text>();
            if (stats != null && Services.Session != null && Services.Score != null)
            {
                var waves = Services.Session.Waves;
                stats.text =
                    $"SCORE   {Services.Score.Score:N0}\n" +
                    $"WAVES   {(waves != null ? waves.WaveNumber.ToString() : "-")}\n" +
                    $"KILLS   {(waves != null ? waves.KillsThisRun.ToString() : "-")}\n" +
                    $"BEST    {Services.Score.BestScore:N0}";
            }
        }

        void HideAll()
        {
            if (victoryRoot != null) victoryRoot.SetActive(false);
            if (defeatRoot != null) defeatRoot.SetActive(false);
        }
    }
}
