using TMPro;
using UnityEngine;

public class MoneyDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text moneyText;

    void Start()
    {
        PlayerInventory.Instance.OnMoneyChanged += UpdateDisplay;
        UpdateDisplay(PlayerInventory.Instance.Money);
    }

    void OnDestroy()
    {
        if (PlayerInventory.Instance != null)
            PlayerInventory.Instance.OnMoneyChanged -= UpdateDisplay;
    }

    private void UpdateDisplay(int amount) => moneyText.text = $"{amount}";
}
