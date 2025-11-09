using UnityEngine;
using System.Collections;
using System;
using Random = UnityEngine.Random;

public class GameManager : MonoBehaviour
{
    // Inspectorからリールをアタッチするためのスロット
    [Header("リール参照")]
    public ReelController leftReel;
    public ReelController centerReel;
    public ReelController rightReel;

    [Header("演出マネージャー参照")]
    public DracoMeteorManager dracoMeteorManager;
    public DescendingNumberManager descendingNumberManager; 

    [Header("ゲーム状態")]
    private bool isSpinning = false;
    
    // 1図柄当たりの角度
    private const float TOTAL_DEGREES = 360f;
    private const int TOTAL_SYMBOLS = 10;
    private const float ANGLE_PER_SYMBOL = TOTAL_DEGREES / TOTAL_SYMBOLS; //36度

    // 最終停止させたい図柄インデックス（0～9)
    [Header("最終停止図柄インデックス (0～9)")]
    public int left_num = 1;
    public int center_num = 1;
    public int right_num = 3;

    [Header("汎用4色点滅設定")]
    public Light targetFlashLight; 
    public Color[] genericFlashColors = new Color[] { Color.red, Color.yellow, Color.cyan, Color.magenta }; 
    public float genericFlashInterval = 0.02f; 
    public float genericMaxIntensity = 10.0f; 
    private Color originalTargetLightColor; 
    private float originalTargetLightIntensity; 

    [Header("点滅時間設定")] 
    public float reachFlashDuration = 1.0f;     
    public float finalFlashDuration = 1.5f;     
    
    [Header("大当たり後の爆発演出")]
    public GameObject explosionEffectPrefab; 
    public Transform explosionSpawnPoint;     
    public float explosionDuration = 2.0f;    
    public float explosionInterval = 0.2f;    
    public Vector3 explosionSpawnOffset = new Vector3(0, 0, 5); 

    [Header("大当たり時のスロット揺らし演出")] 
    public float shakeDuration = 0.5f;   
    public float shakeMagnitude = 0.1f;  
    
    // 演出に使う定数
    private float leftReelInitialSpinTime = 2.0f; 
    private bool sideMatch = false; 
    private bool isTripleMatch = false; 
    private float NORMAL_STOP_OFFSET = 180f;
    private float FLUSH_STOP_OFFSET = 120f;
    
    //流星群(Draco Meteor)を撃つために使う変数
    public Transform reelGroupTransform;
    private Vector3 originalReelPosition;
    private bool isReelGroupRetired = false; 


    void Start()
    {
        if (leftReel == null || rightReel == null || centerReel == null)
        {
            Debug.LogError("リールコントローラがInspectorで設定されていません");
            return;
        }

        // ---  連動ルールの設定（イベントの購読） ---
        leftReel.OnStopCompleted += StartRightDeceleration;
        rightReel.OnStopCompleted += StartCenterDeceleration;
        centerReel.OnStopCompleted += OnAllReelsStopped;

        ResetAllReels();

        if (reelGroupTransform != null)
        {
            originalReelPosition = reelGroupTransform.localPosition;
        }

        // 汎用フラッシュライトの初期設定を保存
        if (targetFlashLight != null)
        {
            originalTargetLightColor = targetFlashLight.color;
            originalTargetLightIntensity = targetFlashLight.intensity;
        }
    }

    // --- メインフロー ---

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) && !isSpinning)
        {
            StartSpinSequence();
        }
    }
    
    private void StartSpinSequence()
    {
        if (isSpinning) return;
        isSpinning = true;

        Debug.Log("GameManager : 全リール回転開始");
        
        // ★オーディオ: 回転スタート効果音を鳴らし、回転中BGMを流す★
        SoundManager.Instance.PlayStartSpinSE(); 
        SoundManager.Instance.PlaySpinningBGM();

        float finalAngleLeft = CalculateFinalAngle(left_num);
        float finalAngleCenter = CalculateFinalAngle(center_num);
        float finalAngleRight = CalculateFinalAngle(right_num);

        sideMatch = (left_num == right_num);

        ResetAllReels();

        leftReel.StartSpin(finalAngleLeft);
        centerReel.StartSpin(finalAngleCenter);
        rightReel.StartSpin(finalAngleRight);

        StartCoroutine(StartLeftDecelerationWithDelay());
    }

    private IEnumerator StartLeftDecelerationWithDelay()
    {
        yield return new WaitForSeconds(leftReelInitialSpinTime); 
        leftReel.StartDeceleration(NORMAL_STOP_OFFSET);
    }

    // 1. 左リール停止後
    private void StartRightDeceleration()
    {
        Debug.Log("GameManager : 左リール停止完了→右リールに減速開始を指示");
        // オーディオ: リール停止音
        SoundManager.Instance.PlayStopReelSE(); 
        rightReel.StartDeceleration();
    }

    // 2. 右リール停止後
    private void StartCenterDeceleration()
    {
        Debug.Log("GameManager : 右リール停止完了→中央リールに減速開始を指示");
        // オーディオ: リール停止音
        SoundManager.Instance.PlayStopReelSE(); 
        
        sideMatch = (left_num == right_num);

        if (sideMatch)
        {
            Debug.Log("GameManager: ドキドキリーチ演出へ！");
            StopAllCoroutines(); 

            // ★オーディオ: 回転中BGMを即時停止し、リーチ演出時BGMに切り替え★
            SoundManager.Instance.PlayReachBGM();

            StartCoroutine(ReachSequence());
        }
        else
        {
            Debug.Log("GameManager: 通常停止。中央リールは通常減速");
            // ★オーディオ: リーチにならなければ、回転中BGMを停止★
            SoundManager.Instance.PlayReachMissBGM(); 
            centerReel.StartDeceleration(NORMAL_STOP_OFFSET);
        }
    }

    // 3. リーチ演出（リール退避前点滅）
    private IEnumerator ReachSequence()
    {
        // オーディオ: リーチ点滅音
        SoundManager.Instance.PlayReachFlashSE(); 

        // 1. 点滅を開始し、完了を待つ (リール退避前)
        if (targetFlashLight != null)
        {
            yield return StartCoroutine(FlashLightCoroutine(reachFlashDuration)); 
        }
        
        // 2. リールグループを退避
        SetReelGroupRetirement(true);
        yield return new WaitForSeconds(1.0f); 
    
        // 3. 大当たり判定と分岐
        isTripleMatch = (left_num == center_num && center_num == right_num);

        if (isTripleMatch)
        {
            Debug.Log("GameManager: 大当たり！流星群と数字降下演出を開始します。");
            
            // ★ここを修正: リーチBGMを停止し、Draco Meteor専用BGMを再生★
            SoundManager.Instance.PlayMeteorBGM(); 
            
            if (dracoMeteorManager != null)
            {
                // DracoMeteorManager内で、隕石落下SE、爆発音SEが流れると想定
                dracoMeteorManager.StartMeteorShower(OnDracoMeteorFinished);
            }
            else
            {
                OnDracoMeteorFinished();
            }
        }
        else
        {
            // ★オーディオ: 外れ時、リーチ外れBGMに切り替え★
            SoundManager.Instance.PlayReachMissBGM(); 
            Debug.Log("GameManager: 残念..!! リーチ外れ。演出をスキップしリール復帰へ。");
            yield return new WaitForSeconds(1.0f); 
            StartCoroutine(ResumeSpinAfterRetirement()); 
        }
    }

    // 4. 流星群完了コールバック
    private void OnDracoMeteorFinished()
    {
        // ★ここを修正: MeteorBGMから数字降下演出BGMへ切り替え★
        SoundManager.Instance.PlayDescendingNumberBGM();
        SoundManager.Instance.PlayDescendingNumberSE();

        if (isTripleMatch && descendingNumberManager != null)
        {
            Debug.Log("GameManager: 流星群完了。数字降下演出開始。");
            int winningIndex = left_num;
            descendingNumberManager.StartDescendingPattern(winningIndex, OnDescendingNumberFinished);
        }
        else
        {
             // 外れルート（ありえない想定）
             StartCoroutine(ResumeSpinAfterRetirement());
        }
    }

    // 5. 数字降下演出完了コールバック
    private void OnDescendingNumberFinished()
    {
        Debug.Log("GameManager: 数字降下演出完了。リール復帰へ。");
        // オーディオ: 数字降下BGMを停止
        SoundManager.Instance.StopBGM();
        StartCoroutine(ResumeSpinAfterRetirement()); 
    }

    // 6. リール復帰と中央リール減速指示
    private IEnumerator ResumeSpinAfterRetirement()
    {
        // 1. リールグループを元の位置に復帰させる
        SetReelGroupRetirement(false);

        // ★オーディオ: リーチ外れBGM（または無音）から最後の回転中用BGMへ切り替え★
        SoundManager.Instance.PlayFinalSpinningBGM(); 

        // 2. 復帰演出の待ち時間を設ける
        yield return new WaitForSeconds(0.3f);

        // 3. 中央リールに減速開始を指示
        float requiredOffset = FLUSH_STOP_OFFSET;
        centerReel.StartDeceleration(requiredOffset); 
        
        Debug.Log("GameManager: リール復帰後、中央リールの最終停止シーケンスを開始しました。");

        yield break;
    }

    // 7. 全リール停止後 (大当たり時のみ最終点滅)
    private void OnAllReelsStopped()
    {
        Debug.Log("GameManager : 全リール停止完了。");
        isSpinning = false;
        
        // オーディオ: 中央リール停止音
        SoundManager.Instance.PlayStopReelSE(); 

        CheckWinCondition();

        // 最終回転中BGMを停止
        SoundManager.Instance.StopBGM();

        if (isTripleMatch)
        {
            // ★オーディオ: 回転が停止し点滅と同時に大当たりBGMを流す★
            SoundManager.Instance.PlayJackpotFanfareBGM();
            
            // 1. 最終点滅演出を開始
            if (targetFlashLight != null)
            {
                StartCoroutine(FlashLightCoroutine(finalFlashDuration)); 
            }

            // 2. 爆発演出を開始
            if (explosionEffectPrefab != null && explosionSpawnPoint != null)
            {
                StartCoroutine(BurstExplosionsBehindReel(explosionDuration));
            }

            // 3. スロット揺らし演出を開始
            if (reelGroupTransform != null)
            {
                StartCoroutine(ReelGroupShakeCoroutine(shakeDuration, shakeMagnitude));
            }

            Debug.Log("3枚一致検出。");
        }
        else
        {
            Debug.Log("何も揃いませんでした。次のゲームへ。");
        }
    }

    // --- 汎用コルーチンとヘルパーメソッド ---
    
    private IEnumerator FlashLightCoroutine(float duration)
    {
        if (targetFlashLight == null || genericFlashColors.Length == 0) yield break;
        float startTime = Time.time;
        int colorIndex = 0;
        while (Time.time < startTime + duration)
        {
            colorIndex = (colorIndex + 1) % genericFlashColors.Length;
            targetFlashLight.color = genericFlashColors[colorIndex];
            targetFlashLight.intensity = genericMaxIntensity;
            yield return new WaitForSeconds(genericFlashInterval / 2f);
            targetFlashLight.intensity = 0f;
            yield return new WaitForSeconds(genericFlashInterval / 2f);
        }
        targetFlashLight.color = originalTargetLightColor;
        targetFlashLight.intensity = originalTargetLightIntensity;
    }

    private IEnumerator ReelGroupShakeCoroutine(float duration, float magnitude)
    {
        if (reelGroupTransform == null) yield break;
        Vector3 originalPos = originalReelPosition;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;
            reelGroupTransform.localPosition = originalPos + new Vector3(x, y, 0);
            elapsed += Time.deltaTime;
            yield return null; 
        }
        reelGroupTransform.localPosition = originalPos;
    }
    
    private IEnumerator BurstExplosionsBehindReel(float totalDuration)
    {
        if (explosionEffectPrefab == null || explosionSpawnPoint == null) yield break;
        float startTime = Time.time;
        while (Time.time < startTime + totalDuration)
        {
            Vector3 spawnPosition = explosionSpawnPoint.position + explosionSpawnOffset;
            GameObject explosion = Instantiate(explosionEffectPrefab, spawnPosition, Quaternion.identity);
            Destroy(explosion, 3.0f); 
            yield return new WaitForSeconds(explosionInterval);
        }
    }

    private void CheckWinCondition()
    {
        isTripleMatch = (left_num == center_num && center_num == right_num);
    }

    public void SetReelGroupRetirement(bool retire)
    {
        if (reelGroupTransform == null) return;
        if (retire && !isReelGroupRetired)
        {
            reelGroupTransform.localPosition = originalReelPosition + new Vector3(0, -100f, 0);
            isReelGroupRetired = true;
        }
        else if (!retire && isReelGroupRetired)
        {
            reelGroupTransform.localPosition = originalReelPosition;
            isReelGroupRetired = false;
        }
    }
    
    private float CalculateFinalAngle(int stopIndex)
    {
        float finalAngle = stopIndex * ANGLE_PER_SYMBOL;
        return finalAngle % TOTAL_DEGREES;
    }

    private void ResetAllReels()
    {
        leftReel.ResetReel();
        rightReel.ResetReel();
        centerReel.ResetReel();
    }
}