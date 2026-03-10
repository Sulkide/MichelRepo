using System;
using System.Linq;
using TMPro;
using UnityEngine;

public class ConsoleView : MonoBehaviour
{
    [SerializeField] private TMP_Text text;
    [SerializeField] private int maxLineCount = 10;

    private int _lineCount = 0;
    private string _myLog;

    private void OnEnable()
    {
        Application.logMessageReceived += Log;
    }

    private void OnDisable()
    {
        Application.logMessageReceived -= Log;
    }

    private void Log(string logString, string stackTrace, LogType type)
    {
        string logColor = type switch
        {
            LogType.Warning => "yellow",
            LogType.Exception or LogType.Error => "red",
            _ => "white"
        };
        
        logString = "<color=" + logColor + ">" + logString + "</color>";

        _myLog += "\n" + logString;
        _lineCount++;

        if (_lineCount > maxLineCount)
        {
            _lineCount--;
            _myLog = DeleteLines(_myLog, 1);
        }

        text.text = _myLog;
    }

    private string DeleteLines(string text, int count)
    {
        return text.Split(Environment.NewLine.ToCharArray(), count + 1).Skip(count).FirstOrDefault();
    }
}