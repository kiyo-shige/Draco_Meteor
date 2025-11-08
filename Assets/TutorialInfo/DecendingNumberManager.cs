using UnityEngine;
using System.Collections;
using System;
using Unity.Cinemachine; 

public class DescendingNumberManager : MonoBehaviour
{
    [Header("雰囲気変更 (神々しさの演出)")]
    // ★空のマテリアル★
    public Material winSkybox;      // 大当たり演出用のSkyboxマテリアル
    private Material originalSkybox; // 元のSkyboxマテリアルを保持
    public float skyboxFadeDuration = 1.0f; // 空の切り替え時間

    // ★ライトの操作★
    public Light sceneDirectionalLight; // シーンのDirectional Light（太陽光）をアタッチ
    public Color winLightColor = Color.yellow;    // 大当たり時の光の色
    public float winLightIntensity = 2.0f;        // 大当たり時の光の強さ
    private Color originalLightColor;
    private float originalLightIntensity;

    // ★降下オブジェクトのエフェクト★
    public GameObject divineGlowPrefab; // 数字にアタッチする神々しい光のエフェクト
    
    [Header("既存の数字/パターンオブジェクト")]
    public GameObject[] patternPrefabs;

    [Header("降下設定")]
    public Vector3 startPosition = new Vector3(0, 50, 0); // 開始位置 (上空)
    public Vector3 endPosition = new Vector3(0, 0, 0);   // 着地位置 (地面)
    public float fallDuration = 10.0f;                    // 降下にかかる時間
    public float scaleFactor = 3.0f;                     // 着地時の最終的なスケール倍率

    [Header("カメラ設定")]
    public CinemachineCamera followCamera; // ★追尾カメラをアタッチ★
    private int originalCameraPriority = 10; // 演出開始前のカメラ優先度（復帰用）
    public float displayTime = 2.0f;       // 着地後の表示時間

    [Header("エフェクト")]
    public GameObject landingEffectPrefab; // 着地時の爆発・光エフェクト
    public float effectDuration = 1.0f;

    public Action OnCompleted;

    [Header("着地時の超点滅設定")]
    public Light landingFlashLight; // 着地時に点滅させるライトをアタッチ
    public Color[] flashColors = new Color[] { Color.red, Color.yellow, Color.cyan, Color.magenta }; // 4色の点滅カラー
    public float landingFlashDuration = 0.5f; // 点滅させる時間
    public float landingFlashInterval = 0.01f; // 超高速点滅の間隔
    public float landingMaxIntensity = 10.0f; // 点滅時の最大強度


    void Start()
    {
        // 元のライト設定を保存
        if (sceneDirectionalLight != null)
        {
            originalLightColor = sceneDirectionalLight.color;
            originalLightIntensity = sceneDirectionalLight.intensity;
        }
        // 元のSkyboxマテリアルを保存
        originalSkybox = RenderSettings.skybox;
    }


    //  降下させるパターンのインデックス (0-9) を受け取る★
    public void StartDescendingPattern(int patternIndex, Action callback)
    {
        OnCompleted = callback;
        StartCoroutine(AnimateDescendingPattern(patternIndex));
    }

    private IEnumerator AnimateDescendingPattern(int patternIndex)
    {
        // 変数の宣言をコルーチンの冒頭で行う
        GameObject descendingObject = null;
        GameObject glowInstance = null;
        GameObject selectedPrefab = null;

        // 1. パターンオブジェクトの選択と初期化
        if (patternIndex < 0 || patternIndex >= patternPrefabs.Length || patternPrefabs[patternIndex] == null)
        {
            Debug.LogWarning($"パターンインデックス {patternIndex} が無効です。");
            OnCompleted?.Invoke();
            yield break;
        }
        selectedPrefab = patternPrefabs[patternIndex];
        
        // ★神々しい演出開始: 空と光を切り替える★
        yield return StartCoroutine(ChangeSkyAtmosphere(true)); 

        // ★オブジェクトを生成し、変数に代入 (宣言は不要)★
        descendingObject = Instantiate(selectedPrefab, startPosition, Quaternion.identity); 

        // ★神々しい光のエフェクトを数字の子として生成し、追従させる★
        if (divineGlowPrefab != null)
        {
            glowInstance = Instantiate(divineGlowPrefab, descendingObject.transform);
            glowInstance.transform.localPosition = Vector3.zero;
        }


        Debug.Log($"天からパターン (Index: {patternIndex}) が降下中...");

        Quaternion originalRotation = descendingObject.transform.rotation;
        Vector3 initialScale = descendingObject.transform.localScale;

        // --- 2. カメラ追尾開始 ---
        if (followCamera != null)
        {
            originalCameraPriority = followCamera.Priority;
            followCamera.Follow = descendingObject.transform; 
            followCamera.LookAt = descendingObject.transform;
            followCamera.Priority = 40; 
            yield return null; // カメラ切り替えを待つ
        }

        // --- 3. 降下アニメーション ---
        float timer = 0f;
        while (timer < fallDuration)
        {
            timer += Time.deltaTime;
            float t = timer / fallDuration;

            descendingObject.transform.position = Vector3.Lerp(startPosition, endPosition, t);
            descendingObject.transform.localScale = Mathf.Lerp(1.0f, scaleFactor, t) * initialScale;

            yield return null;
        }

        // 最終位置とスケールに設定
        descendingObject.transform.position = endPosition;
        descendingObject.transform.localScale = initialScale * scaleFactor;

        // --- 4. 着地エフェクト ---

        // ★キラキラ（グローエフェクト）の消去処理をここで実行★
        if (glowInstance != null)
        {
            Destroy(glowInstance); 
            Debug.Log("着地と同時に神々しいキラキラを消去しました。");
        }
        
        // 着地時の超点滅演出を開始
        StartCoroutine(LandingFlashCoroutine(landingFlashDuration));

        if (landingEffectPrefab != null)
        {
            GameObject landingEffect = Instantiate(landingEffectPrefab, endPosition, originalRotation);
            Destroy(landingEffect, effectDuration);
        }

        // ★神々しい演出終了: 空と光を元に戻す★
        yield return StartCoroutine(ChangeSkyAtmosphere(false));


        // 5. 着地後しばらく表示
        yield return new WaitForSeconds(displayTime);

        // --- 6. 演出終了とカメラ復帰 ---

        
        // カメラのプライオリティを元に戻す
        if (followCamera != null)
        {
            followCamera.Priority = originalCameraPriority;
            yield return null; // カメラ復帰を待つ
        }

        Destroy(descendingObject); // 生成したオブジェクトを破棄

        Debug.Log("天からの数字演出完了。");
        OnCompleted?.Invoke();
    }

    private IEnumerator ChangeSkyAtmosphere(bool toWinState)
    {
        float timer = 0f;
        Color startColor = toWinState ? originalLightColor : winLightColor;
        float startIntensity = toWinState ? originalLightIntensity : winLightIntensity;
        Color endColor = toWinState ? winLightColor : originalLightColor;
        float endIntensity = toWinState ? winLightIntensity : originalLightIntensity;
        Material targetSkybox = toWinState ? winSkybox : originalSkybox;

        while (timer < skyboxFadeDuration)
        {
            float t = timer / skyboxFadeDuration;

            if (sceneDirectionalLight != null)
            {
                sceneDirectionalLight.color = Color.Lerp(startColor, endColor, t);
                sceneDirectionalLight.intensity = Mathf.Lerp(startIntensity, endIntensity, t);
            }

            timer += Time.deltaTime;
            yield return null;
        }
        
        // 最終状態に確定
        if (sceneDirectionalLight != null)
        {
            sceneDirectionalLight.color = endColor;
            sceneDirectionalLight.intensity = endIntensity;
        }

        if (targetSkybox != null)
        {
            RenderSettings.skybox = targetSkybox;
            DynamicGI.UpdateEnvironment();
        }
    }

    private IEnumerator LandingFlashCoroutine(float duration)
    {
        if (landingFlashLight == null || flashColors.Length == 0) yield break;

        // 元の色と強度を保存 (終了後に戻すため)
        Color originalColor = landingFlashLight.color;
        float originalIntensity = landingFlashLight.intensity;
        
        float startTime = Time.time;
        int colorIndex = 0;

        // 💡 超高速点滅開始
        while (Time.time < startTime + duration)
        {
            // 1. ON: 色と最大強度を設定
            colorIndex = (colorIndex + 1) % flashColors.Length;
            landingFlashLight.color = flashColors[colorIndex];
            landingFlashLight.intensity = landingMaxIntensity;
            
            // 超短時間待機 (点滅間隔の半分)
            yield return new WaitForSeconds(landingFlashInterval / 2f);
            
            // 2. OFF: 瞬間的に強度をゼロにする
            landingFlashLight.intensity = 0f; 
            
            // 超短時間待機 (点滅間隔の残り半分)
            yield return new WaitForSeconds(landingFlashInterval / 2f);
        }

        // 演出終了: 元の設定に戻す
        landingFlashLight.color = originalColor; 
        landingFlashLight.intensity = originalIntensity;
    }
}