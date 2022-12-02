using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DebugScreen : MonoBehaviour
{
    public static DebugScreen Instance { get; private set; }

#pragma warning disable 0414
    [SerializeField] GameObject root = null;
#pragma warning restore 0414
    [SerializeField] Text textField = null;

    private readonly List<Func<string>> entries = new List<Func<string>>();

    private void Start()
    {
        Instance = this;

        textField.text = String.Empty;

#if !UNITY_EDITOR
        root.Destroy();
#endif
    }

    public void AddDebugEntry( Func<string> setFunc )
    {
        entries.Add( setFunc );
        Update();
    }

    private void Update()
    {
        var str = string.Empty;
        foreach( var x in entries )
            str += x() + "\n";
        textField.text = str;
    }
}
