using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;
using System.Text.RegularExpressions;
using rnd = UnityEngine.Random;

public class smallCircle : MonoBehaviour
{
    public new KMAudio audio;
    public KMBombInfo bomb;
    public KMBombModule module;

    public KMSelectable[] wedges;
    public KMSelectable reset;
    public Renderer[] wedgeRenders;
    public Color[] colors;
    public Color gray;
    public Transform circle;
    public TextMesh colorblindText;

    private bool stage2;
    private int[] wedgeColors = new int[8];
    private int[] solution = new int[3];
    private int shift;
    private int tableColor;
    private int substage;

    private bool isCcw;
    private bool firstTime = true;
    private int pressedTime;
    private int releasedTime;
    private Coroutine[] fadingAnimations = new Coroutine[8];
    private bool[] fading = new bool[8];
    private float circleSpeed = 20f;

    private static readonly string[] colorNames = new string[] { "red", "orange", "yellow", "green", "blue", "magenta", "white", "black" };
    private static readonly string[][] table = new string[8][]
    {
        new string[8] { "ADF", "FBG", "DEF", "FCE", "GCH", "GEB", "BCE", "BAC" },
        new string[8] { "CFB", "FHD", "EAB", "HGB", "HDE", "AGD", "EDH", "FGA" },
        new string[8] { "CBA", "HCE", "DEA", "EFB", "FAH", "CFH", "EAF", "FAC" },
        new string[8] { "AEC", "CBF", "GCB", "AFD", "EHG", "AEF", "AGF", "EGH" },
        new string[8] { "GEB", "ACH", "EHA", "CDB", "EFD", "DGH", "BDE", "FGD" },
        new string[8] { "FEC", "GBC", "DFC", "FGH", "ACF", "DGH", "DGF", "FEH" },
        new string[8] { "GFC", "BCE", "BFC", "HFD", "ADB", "GCD", "GBH", "AGE" },
        new string[8] { "AGH", "HDB", "DGF", "AGD", "EDG", "AGF", "FGC", "FBE" }
    };

    private static int moduleIdCounter = 1;
    private int moduleId;
    private bool moduleSolved;

    private void Awake()
    {
        moduleId = moduleIdCounter++;
        foreach (KMSelectable wedge in wedges)
        {
            wedge.OnInteract += delegate () { PressWedge(wedge); return false; };
            wedge.OnInteractEnded += delegate () { ReleaseWedge(wedge); };
            wedge.OnHighlight += delegate () { colorblindText.text = "ROYGBMWK"[wedgeColors[Array.IndexOf(wedges, wedge)]].ToString(); };
            wedge.OnHighlightEnded += delegate () { colorblindText.text = ""; };
        }
        reset.OnInteract += delegate () { PressReset(); return false; };
        isCcw = rnd.Range(0, 2) == 0;
        colorblindText.gameObject.SetActive(GetComponent<KMColorblindMode>().ColorblindModeActive);
    }

    private void Start()
    {
        wedgeColors = Enumerable.Range(0, 8).ToList().Shuffle().ToArray();
        for (int i = 0; i < 8; i++)
        {
            if (firstTime)
                wedgeRenders[i].material.color = gray;
            fadingAnimations[i] = StartCoroutine(FadeColor(wedgeRenders[i], wedgeRenders[i].material.color, colors[wedgeColors[i]], 1f));
        }
        firstTime = false;
        Debug.LogFormat("[Small Circle #{0}] The colors going clockwise starting from the one that makes a different sound are: {1}.", moduleId, wedgeColors.Select(x => colorNames[x]).Join(", "));
        StartCoroutine(Rotate());
    }

    private void PressWedge(KMSelectable wedge)
    {
        var ix = Array.IndexOf(wedges, wedge);
        audio.PlayGameSoundAtTransform(ix != 0 ? KMSoundOverride.SoundEffect.ButtonPress : KMSoundOverride.SoundEffect.ButtonRelease, wedge.transform);
        if ((Application.isEditor && ix == 0) || (rnd.Range(0, 500) == 0 && ix == 0))
            audio.PlaySoundAtTransform("bonk", wedge.transform);
        if (moduleSolved)
            return;
        pressedTime = ((int)bomb.GetTime()) % 60;
        if (stage2)
        {
            Debug.LogFormat("[Small Circle #{0}] You pressed {1}.", moduleId, colorNames[wedgeColors[ix]]);
            if (wedgeColors[ix] == solution[substage])
                substage++;
            else
            {
                module.HandleStrike();
                Debug.LogFormat("[Small Circle #{0}] That was incorrect. Strike!", moduleId);
            }
            if (substage == 3)
            {
                module.HandlePass();
                moduleSolved = true;
                Debug.LogFormat("[Small Circle #{0}] Module solved!", moduleId);
                colorblindText.gameObject.SetActive(false);
                for (int i = 0; i < 8; i++)
                    fadingAnimations[i] = StartCoroutine(FadeColor(wedgeRenders[i], wedgeRenders[i].material.color, gray, 2.5f));
                StartCoroutine(SlowDownCircle());
            }
        }
    }

    private void ReleaseWedge(KMSelectable wedge)
    {
        if (moduleSolved)
            return;
        releasedTime = ((int)bomb.GetTime()) % 60;
        if (!stage2 && pressedTime != releasedTime)
        {
            Debug.LogFormat("[Small Circle #{0}] Stage 2 initiated...", moduleId);
            stage2 = true;
            for (int i = 0; i < 8; i++)
            {
                if (fadingAnimations[i] != null)
                {
                    StopCoroutine(fadingAnimations[i]);
                    fadingAnimations[i] = null;
                }
            }
            shift = rnd.Range(1, 9);
            Debug.LogFormat("[Small Circle #{0}] The sequence is shifted by {1}.", moduleId, shift);
            tableColor = wedgeColors[0];
            wedgeColors = Shift(wedgeColors, isCcw ? shift : 8 - shift);
            var str = table[shift - 1][tableColor];
            Debug.LogFormat("[Small Circle #{0}] Using rules: {1}", moduleId, str);
            for (int i = 0; i < 3; i++)
            {
                switch (str[i])
                {
                    case 'A':
                        solution[i] = (Adjacent(0, 2) || Adjacent(0, 6)) ? 3 : 5;
                        break;
                    case 'B':
                        var opposite = wedgeColors[(Array.IndexOf(wedgeColors, 1) + 4) % 8];
                        solution[i] = (opposite == 5 || opposite == 7) ? 4 : 0;
                        break;
                    case 'C':
                        solution[i] = (!Adjacent(2, 1) && !Adjacent(2, 3) && !Adjacent(2, 5)) ? 7 : 1;
                        break;
                    case 'D':
                        var distanceD = Math.Abs(Array.IndexOf(wedgeColors, tableColor) - Array.IndexOf(wedgeColors, 3));
                        switch (distanceD)
                        {
                            case 7:
                            case 1:
                                distanceD = 0;
                                break;
                            case 6:
                            case 2:
                                distanceD = 1;
                                break;
                            case 5:
                            case 3:
                                distanceD = 2;
                                break;
                            case 4:
                                distanceD = 3;
                                break;
                        }
                        solution[i] = distanceD < bomb.GetBatteryCount() ? 6 : 2;
                        break;
                    case 'E':
                        if (i == 0)
                            solution[i] = 7;
                        else if (i == 1)
                            solution[i] = solution[0] == 4 ? 1 : 7;
                        else if (i == 2)
                            solution[i] = solution[0] == 4 || solution[1] == 4 ? 1 : 7;
                        break;
                    case 'F':
                        if (i == 0)
                            solution[i] = 4;
                        else if (i == 1)
                            solution[i] = solution[0] == 5 ? 6 : 4;
                        else if (i == 2)
                            solution[i] = solution[0] == 5 || solution[1] == 5 ? 6 : 4;
                        break;
                    case 'G':
                        solution[i] = (Adjacent(6, 0) || Adjacent(6, 2) || Adjacent(6, 4)) ? 2 : 5;
                        break;
                    case 'H':
                        var distanceH = Math.Abs(Array.IndexOf(wedgeColors, tableColor) - Array.IndexOf(wedgeColors, 7));
                        switch (distanceH)
                        {
                            case 7:
                            case 1:
                                distanceH = 6;
                                break;
                            case 6:
                            case 2:
                                distanceH = 5;
                                break;
                            case 5:
                            case 3:
                                distanceH = 4;
                                break;
                            case 4:
                                distanceH = 3;
                                break;
                        }
                        solution[i] = distanceH > bomb.GetSerialNumberNumbers().Last() ? 0 : 3;
                        break;
                    default:
                        throw new InvalidOperationException("Unexpected value in the rules.");
                }
            }
            Debug.LogFormat("[Small Circle #{0}] Solution: {1}.", moduleId, solution.Select(x => colorNames[x]).Join(", "));
            StartCoroutine(StageTwo());
        }
    }

    private IEnumerator StageTwo()
    {
        colorblindText.gameObject.SetActive(false);
        foreach (Renderer wedge in wedgeRenders)
            fadingAnimations[Array.IndexOf(wedgeRenders, wedge)] = StartCoroutine(FadeColor(wedge, wedge.material.color, gray, 1f));
        yield return new WaitUntil(() => !fading.Contains(true));
        yield return new WaitForSeconds(.5f);
        foreach (Renderer wedge in wedgeRenders)
            fadingAnimations[Array.IndexOf(wedgeRenders, wedge)] = StartCoroutine(FadeColor(wedge, gray, colors[wedgeColors[Array.IndexOf(wedgeRenders, wedge)]], 1f));
        colorblindText.gameObject.SetActive(GetComponent<KMColorblindMode>().ColorblindModeActive);
    }

    private void PressReset()
    {
        audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, reset.transform);
        if (moduleSolved)
            return;
        Debug.LogFormat("[Small Circle #{0}] Module reset...", moduleId);
        stage2 = false;
        substage = 0;
        StopAllCoroutines();
        for (int i = 0; i < 8; i++)
            fadingAnimations[i] = null;
        Start();
    }

    private IEnumerator Rotate()
    {
        while (true)
        {
            var framerate = 1f / Time.deltaTime;
            var rotation = circleSpeed / framerate;
            if (isCcw)
                rotation *= -1;
            var y = circle.localEulerAngles.y;
            y += rotation;
            circle.localEulerAngles = new Vector3(0f, y, 0f);
            yield return null;
        }
    }

    private IEnumerator FadeColor(Renderer wedge, Color start, Color end, float duration)
    {
        fading[Array.IndexOf(wedgeRenders, wedge)] = true;
        var elapsed = 0f;
        while (elapsed < duration)
        {
            wedge.material.color = Color.Lerp(start, end, elapsed / duration);
            yield return null;
            elapsed += Time.deltaTime;
        }
        wedge.material.color = end;
        fading[Array.IndexOf(wedgeRenders, wedge)] = false;
    }

    private IEnumerator SlowDownCircle()
    {
        var elapsed = 0f;
        var duration = 2f;
        while (elapsed < duration)
        {
            circleSpeed = Mathf.Lerp(20f, 0f, elapsed / duration);
            yield return null;
            elapsed += Time.deltaTime;
        }
        circleSpeed = 0f;
    }

    private static int[] Shift(int[] sequence, int x)
    {
        var str = sequence.Join("");
        str = str.Substring(str.Length - x) + str.Substring(0, str.Length - x);
        for (int i = 0; i < sequence.Length; i++)
            sequence[i] = Int32.Parse(str[i].ToString());
        return sequence;
    }

    private bool Adjacent(int color1, int color2)
    {
        var ix1 = Array.IndexOf(wedgeColors, color1);
        var ix2 = Array.IndexOf(wedgeColors, color2);
        if (ix1 == 0)
            return ix2 == 1 || ix2 == 7;
        else if (ix1 == 7)
            return ix2 == 0 || ix2 == 6;
        else
            return ix2 == ix1 + 1 || ix2 == ix1 - 1;
    }

    // Twitch Plays
#pragma warning disable 414
    private readonly string TwitchHelpMessage = "!{0} cycle <color> [Taps every wedge going clockwise from the specified color once.] !{0} go [If not in stage 2, enters stage 2.] !{0} <color> <color> <color> [If in stage 2, submits those 3 colors.] !{0} reset [Resets the module.]";
#pragma warning restore 414

    private IEnumerator ProcessTwitchCommand(string input)
    {
        input = input.ToLowerInvariant();
        var split = input.Split(' ');
        var validColors = new string[] { "red", "orange", "yellow", "green", "blue", "magenta", "white", "black", "r", "o", "y", "g", "b", "m", "w", "k", "purple", "violet", "p", "v", "lime", "l" };
        if (split.Length == 1 && !validColors.Contains(input))
        {
            switch (input)
            {
                case "go":
                case "start":
                case "begin":
                    if (stage2)
                    {
                        yield return "sendtochaterror It's already stage 2.";
                        yield break;
                    }
                    yield return null;
                    wedges[0].OnInteract();
                    var time = (int)bomb.GetTime();
                    yield return new WaitUntil(() => (int)bomb.GetTime() != time);
                    wedges[0].OnInteractEnded();
                    break;
                case "reset":
                    yield return null;
                    reset.OnInteract();
                    break;
                default:
                    yield break;
            }
        }
        else if (split.Length == 2 && split.ToArray()[0] == "cycle" && validColors.Contains(split.ToArray()[1]))
        {
            if (stage2)
            {
                yield return "sendtochaterror Should've done that when you had the chance, mate. (Reset to access stage 1 again.)";
                yield break;
            }
            yield return null;
            var color = 0;
            switch (split.ToArray()[1])
            {
                case "red":
                case "r":
                    color = 0;
                    break;
                case "orange":
                case "o":
                    color = 1;
                    break;
                case "yellow":
                case "y":
                    color = 2;
                    break;
                case "green":
                case "g":
                case "lime":
                case "l":
                    color = 3;
                    break;
                case "blue":
                case "b":
                    color = 4;
                    break;
                case "magenta":
                case "m":
                case "purple":
                case "p":
                case "violet":
                case "v":
                    color = 5;
                    break;
                case "white":
                case "w":
                    color = 6;
                    break;
                case "black":
                case "k":
                    color = 7;
                    break;
                default:
                    yield break;
            }
            var x = Array.IndexOf(wedgeColors, color);
            for (int i = 0; i < 8; i++)
            {   
                wedges[x].OnInteract();
                x = (x + 9) % 8;
                yield return "trycancel";
                yield return new WaitForSeconds(1f);
            }
        }
        else if (split.Length <= 3 && split.All(x => validColors.Contains(x)))
        {
            if (!stage2)
            {
                yield return "sendtochaterror Slow down, you're not in stage 2 yet!";
                yield break;
            }
            yield return null;
            var color = 0;
            for (int i = 0; i < split.Length; i++)
            {
                switch (split.ToArray()[i])
                {
                    case "red":
                    case "r":
                        color = 0;
                        break;
                    case "orange":
                    case "o":
                        color = 1;
                        break;
                    case "yellow":
                    case "y":
                        color = 2;
                        break;
                    case "green":
                    case "g":
                    case "lime":
                    case "l":
                        color = 3;
                        break;
                    case "blue":
                    case "b":
                        color = 4;
                        break;
                    case "magenta":
                    case "m":
                    case "purple":
                    case "p":
                    case "violet":
                    case "v":
                        color = 5;
                        break;
                    case "white":
                    case "w":
                        color = 6;
                        break;
                    case "black":
                    case "k":
                        color = 7;
                        break;
                    default:
                        yield break;
                }
                wedges[Array.IndexOf(wedgeColors, color)].OnInteract();
                yield return new WaitForSeconds(.2f);
            }
        }
        else
            yield break;
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        if (stage2)
            goto startStage2;
        var time = (int)bomb.GetTime();
        wedges[0].OnInteract();
        yield return new WaitUntil(() => (int)bomb.GetTime() != time);
        wedges[0].OnInteractEnded();
    startStage2:
        while (!moduleSolved)
        {
            wedges[Array.IndexOf(wedgeColors, solution[substage])].OnInteract();
            yield return new WaitForSeconds(.2f);
        }
    }
}
