using System.Runtime.InteropServices;
using UnityEngine;

public class Javascript : MonoBehaviour
{
    [DllImport( "__Internal" )]
    public static extern string BrowserTextUpload( string extFilter, string gameObjName, string dataSinkFn );
}