using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using TMPro;
using UnityEngine;

public class DamageTextView : MonoBehaviour
{
    [Header("Damage Text")]
    [SerializeField] private TextMeshProUGUI damageText;
    
    [Header("Animation Settings")]
    [SerializeField] private float duration = 1.0f;
    [SerializeField] private float moveSpeed = 100f;
    
    public Subject<DamageTextView> OnAnimationFinished { get; } = new();


    public void Init(int amount, bool isHeal)
    {
        if (isHeal)
        {
            damageText.text = "+" + amount.ToString();
            damageText.color = Color.green;
        }
        else
        {
            damageText.text = "-" + amount.ToString();
            damageText.color = Color.red;
        }
    }

    public async UniTaskVoid PlayAnimation()
    {
        float elapsed = 0f;

        Color startColor = damageText.color;
        CancellationToken token = this.GetCancellationTokenOnDestroy();

        try
        {
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float normalizedTime = elapsed / duration;

                transform.position += Vector3.up * (moveSpeed * Time.deltaTime);

                startColor.a = Mathf.Lerp(1f, 0f, normalizedTime);
                damageText.color = startColor;

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            OnAnimationFinished.OnNext(this);
        }
        catch (OperationCanceledException)
        {
            Debug.Log("애니메이션 종료");
        }
    }

    private void OnDestroy()
    {
        OnAnimationFinished.Dispose();
    }
}