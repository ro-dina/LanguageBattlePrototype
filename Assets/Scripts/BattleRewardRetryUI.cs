using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BattleRewardRetryUI : MonoBehaviour
{
    [SerializeField] private TMP_Text message;
    [SerializeField] private Button retry;
    public void Bind(string reason,Action action)
    {
        message.text = "Unable to save progress.\nYour Coins and items have not changed.\nRetry to continue.";
        Debug.LogWarning("Battle reward save postponed: " + reason);
        retry.onClick.RemoveAllListeners();
        retry.onClick.AddListener(() => action());
    }
}
