using UnityEngine;
using System.Collections;
using System;
public class GameManager : MonoBehaviour
{
    // Inspectorからリールをアタッチするためのスロット
    [Header("リール参照")]
    // ReelControllerをベースにした共通インターフェースを推奨
    public ReelController leftReel;   
    public ReelController centerReel;
    public ReelController rightReel;

    [Header("演出マネージャー参照")]
    public DracoMeteorManager dracoMeteorManager;
    public DescendingNumberManager descendingNumberManager; // ★この行を追加★

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

    [Header("ライト点滅設定")]
// ★点滅させたいライトコンポーネントをアタッチします (Directional Light推奨)★
    public Light targetLight; 
    public float flashDuration = 0.8f;     // 高速フラッシュの時間
    public float flashInterval = 0.02f;    // 超高速切り替えの間隔（0.02秒ごと）
    public float maxIntensity = 8.0f;      // 点滅時の最大強度（元の強度が1の場合、8まで増幅）
    public float fadeDuration = 0.7f;      // フラッシュ後のフェードアウト時間


   

    // 演出に使う定数
    private float leftReelInitialSpinTime = 2.0f; //左リールを等速回転させる時間
    private bool sideMatch = false; //右リールと左リールが一致しているか
    private bool isTripleMatch = false; //全リールが一致しているか
    private float NORMAL_STOP_OFFSET = 180f;
    private float FLUSH_STOP_OFFSET = 180f;
    


    //流星群(Draco Meteor)を撃つために使う変数
    public Transform reelGroupTransform; 
    private Vector3 originalReelPosition; 
    private bool isReelGroupRetired = false; // 退避状態を保持するフラグ★


    void Start()
    {
        if (leftReel == null || rightReel == null || centerReel == null)
        {
            Debug.LogError("リールコントローラがInspectorで設定されていません");
            return;
        }

        // ---  連動ルールの設定（イベントの購読） ---

        // 1. 左リール停止後 → 右リールに減速開始を指示
        leftReel.OnStopCompleted += StartRightDeceleration;

        // 2. 右リール停止後 → 中央リールに減速開始を指示
        rightReel.OnStopCompleted += StartCenterDeceleration;

        // 3. 中央リール停止後 → 全体停止処理へ
        centerReel.OnStopCompleted += OnAllReelsStopped;

        ResetAllReels();

        if (reelGroupTransform != null)
        {
            originalReelPosition = reelGroupTransform.localPosition;
        }
    
    }

    private IEnumerator StartLeftDecelerationWithDelay()
{
    Debug.Log($"左リールは {leftReelInitialSpinTime} 秒後に減速を開始します。");
    // 指定された時間だけ待機 (この間、リールは initialSpeed で回り続ける)
    yield return new WaitForSeconds(leftReelInitialSpinTime); 
    
    // 待機後、左リールに減速命令を出す
    leftReel.StartDeceleration(NORMAL_STOP_OFFSET);
}

    // メイン処理


    //回転状態
    void Update()
    {
        // スペースキーが押されたか確認
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

        // 各リールの最終目標角度
        float finalAngleLeft = CalculateFinalAngle(left_num);
        float finalAngleCenter = CalculateFinalAngle(center_num);
        float finalAngleRight = CalculateFinalAngle(right_num);

        sideMatch = (left_num == right_num);
        Debug.Log("sideMatch : {sideMatch}");
 

        // 1. 全リールをリセット
        ResetAllReels();

        // 2. 全リールに回転開始命令
        // ここでは、各リールに最終角度を渡し、無限回転コルーチンを開始させる
        leftReel.StartSpin(finalAngleLeft);
        centerReel.StartSpin(finalAngleCenter);
        rightReel.StartSpin(finalAngleRight);

        StartCoroutine(StartLeftDecelerationWithDelay());
    }

    // 1. 左リール停止後
    private void StartRightDeceleration()
    {
        Debug.Log("GameManager : 左リール停止完了→右リールに減速開始を指示");
        //右リールに減速開始命令を指示
        rightReel.StartDeceleration();
    }

    // 2. 右リール停止後
    private void StartCenterDeceleration()
    {
        Debug.Log("GameManager : 右リール停止完了→中央リールに減速開始を指示");
        //中央リールに減速開始命令を指示

        float requiredOffset = NORMAL_STOP_OFFSET;
        sideMatch = (left_num == right_num);

        if (sideMatch)
        {
            Debug.Log("GameManager: ドキドキ演出！");

            // 中央リールを保留し、演出コルーチンを開始
            StopAllCoroutines();
            StartCoroutine(ReachSequence());
        }
        else
        {
            Debug.Log("GameManager: 通常停止。中央リールは通常減速");
            centerReel.StartDeceleration(requiredOffset);
        }


    }

    private IEnumerator ReachSequence()
    {
        // 1. リールグループを退避

        if (targetLight != null)
        {
            yield return StartCoroutine(FlashLightCoroutine()); // 点滅を開始
        }
        else
        {
            yield return new WaitForSeconds(flashDuration + fadeDuration);
        }

        SetReelGroupRetirement(true);
        yield return new WaitForSeconds(3.0f); // 退避演出の待ち時間
    

        isTripleMatch = (left_num == center_num && center_num == right_num);

        if (isTripleMatch)
        {
            Debug.Log("GameManager: 大当たり！流星群が襲い掛かる...！！");
            if (dracoMeteorManager != null)
            {
                dracoMeteorManager.StartMeteorShower(OnDracoMeteorFinished);
            }
            else
            {
                OnDracoMeteorFinished();
            }
        }
        else
        {
            //リーチどまり
            Debug.Log("GameManager: 残念..!!リーチどまり!!");
            yield return new WaitForSeconds(1.5f);
            OnDracoMeteorFinished();
        }
    }

    // GameManager.cs のクラス内に追加

    private IEnumerator FlashLightCoroutine()
    {
        if (targetLight == null)
        {
            Debug.LogWarning("点滅用ライトがアタッチされていません。");
            yield break;
        }

        float originalIntensity = targetLight.intensity; // 元の強度を保持

        // 最大強度が元の強度より低い場合、元の強度を最大強度として扱う
        float flashOnIntensity = Mathf.Max(originalIntensity, maxIntensity);
        float flashOffIntensity = originalIntensity; // OFF時は元の強度に戻す

        float startTime = Time.time;
        bool isOn = false;

        // --- 1. 💥 高速パチンコフラッシュ ---
        while (Time.time < startTime + flashDuration)
        {
            isOn = !isOn; // ON/OFFを切り替え

            // 強度を切り替え
            targetLight.intensity = isOn ? flashOnIntensity : flashOffIntensity;

            // 超短時間待機
            yield return new WaitForSeconds(flashInterval);
        }

        // --- 2. 💨 ゆっくりフェードアウト ---
        float timer = 0f;
        float startIntensity = targetLight.intensity;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float t = timer / fadeDuration;

            // 現在の強度から元の強度へ Lerp (補間)
            targetLight.intensity = Mathf.Lerp(startIntensity, originalIntensity, t);

            yield return null;
        }

        // 演出終了: 最終的に元の強度に戻す
        targetLight.intensity = originalIntensity;
    }

    private void OnDracoMeteorFinished()
    {
        if (descendingNumberManager != null)
        {
            // 左右リールで確定している数字のインデックス (0-9) をパターンとして使用
            // これは全リール一致が前提なので、left_num を使います。
            int winningIndex = left_num;

            // 当選インデックスと完了時のコールバックを渡す
             Debug.Log("GameManager: 数字降下演出開始。");
            descendingNumberManager.StartDescendingPattern(winningIndex, OnDescendingNumberFinished);
        }
        else
        {
            // マネージャーがない場合は、直接リール復帰へ
           OnDescendingNumberFinished();
        }
    }

    private void OnDescendingNumberFinished()
    {
        Debug.Log("GameManager: 数字降下演出完了。リール復帰へ。");
        // 演出完了後、リールのスピン再開処理を呼び出す
        StartCoroutine(ResumeSpinAfterRetirement()); // ★このメソッドを定義★
    }

    // GameManager.cs のクラス内に追加

    private IEnumerator ResumeSpinAfterRetirement()
    {
        // リールグループを元の位置に復帰させる
        SetReelGroupRetirement(false);

        // 復帰演出の待ち時間を設ける（例: 0.3秒）
        yield return new WaitForSeconds(0.3f);

        // 中央リールを停止させるロジックへ移行（既存の StartCenterReelStop などを呼び出す）
        float requiredOffset = FLUSH_STOP_OFFSET; 

    // 中央リールに減速開始命令を指示
    centerReel.StartDeceleration(90f); 
    
    Debug.Log("GameManager: リール復帰後、中央リールの最終停止シーケンスを開始しました。");

        // 4. このコルーチンはここで終了
    

        yield break; // 処理の終了
    }



    // 3. 全リール停止後
    private void OnAllReelsStopped()
    {
        Debug.Log("GameManager : 全リール停止完了。勝利判定へ。");
        isSpinning = false;

        CheckWinCondition();

        if (isTripleMatch)
        {
            // 3枚一致した場合、リール退避処理へ移行
            Debug.Log("3枚一致検出。リールを退避させ、後続処理（例えば払い出し/演出）へ移行します。");
            SetReelGroupRetirement(true);

            // ★ここから、退避後の何らかの処理（払い出し、超大当たりロゴ表示など）を開始する★
            // StartCoroutine(AfterRetirementProcess()); 
        }
        else
        {
            // 揃わなかった場合の処理
            Debug.Log("何も揃いませんでした。次のゲームへ。");
        }
    }


    private void ResetAllReels()
    {
        leftReel.ResetReel();
        rightReel.ResetReel();
        centerReel.ResetReel();
    }

    private float CalculateFinalAngle(int stopIndex)
    {
        // 10個の図柄に基づき、インデックスに応じた目標角度 (0°〜360°) を正確に計算
        float finalAngle = stopIndex * ANGLE_PER_SYMBOL;

        return finalAngle % TOTAL_DEGREES;
    }

    // ★追加: リールグループを退避/復帰させるメソッド★

    public void SetReelGroupRetirement(bool retire)
    {
        if (reelGroupTransform == null) return;

        // リールが現在退避状態ではない場合にのみ実行
        if (retire && !isReelGroupRetired)
        {
            // 退避：カメラ外へ移動
            // 瞬間的な移動を想定。値はシーンに合わせて調整
            reelGroupTransform.localPosition = originalReelPosition + new Vector3(0, -100f, 0);
            isReelGroupRetired = true;
            Debug.Log("リールグループを画面外へ退避しました。");
        }
        else if (!retire && isReelGroupRetired)
        {
            // 復帰：元の位置に戻す
            reelGroupTransform.localPosition = originalReelPosition;
            isReelGroupRetired = false;
            Debug.Log("リールグループを元の位置に復帰しました。");
        }
    }

    

    private void CheckWinCondition()
    {
        isTripleMatch = false;

        // 全リールの停止図柄インデックスが一致するか確認
        if (left_num == center_num && center_num == right_num)
        {
            isTripleMatch = true;
            Debug.Log($"💥 3枚完全一致を検出! 図柄インデックス: {left_num}");
        }
        else
        {
            Debug.Log("一致なし。");
        }
    }
    
}