using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.UI;

public class EmoticonView : MonoBehaviour
{
    [SerializeField] private Image emoticonImage;
    public RectTransform RectTransform { get; private set; }

    public Subject<EmoticonView> OnFinished { get; } = new();
    private CancellationTokenSource _cts;

    private void Awake()
    {
        RectTransform = GetComponent<RectTransform>();
    }

    public async UniTaskVoid PlayAnimation(Sprite sprite)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        emoticonImage.sprite = sprite;
        emoticonImage.color = Color.white;
        transform.localScale = Vector3.zero;

        try
        {
            float t = 0;
            while (t < 0.2f)
            {
                t += Time.deltaTime;
                float scale = Mathf.Lerp(0, 1.2f, t / 0.2f);
                transform.localScale = Vector3.one * scale;
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
            transform.localScale = Vector3.one;

            await UniTask.Delay(2000, cancellationToken: token);

            t = 0;
            while (t < 0.3f)
            {
                t += Time.deltaTime;
                Color c = emoticonImage.color;
                c.a = Mathf.Lerp(1, 0, t / 0.3f);
                emoticonImage.color = c;
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            OnFinished.OnNext(this);
        }
        catch (OperationCanceledException)
        {
            Debug.Log("이모지 애니메이션 종료");
        }
    }
    
    private void OnDestroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        OnFinished.Dispose();
    }
}