using UnityEngine;

[CreateAssetMenu(fileName = "RohitGoogleSheetsAnalytics", menuName = "Rohit/Analytics/Google Sheets Settings")]
public class RohitGoogleSheetsSettings : ScriptableObject
{
    public bool uploadEnabled = true;
    public string webAppUrl = "";
    public string sharedSecret = "";
}
