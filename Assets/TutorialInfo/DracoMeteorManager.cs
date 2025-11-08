using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;
using Unity.Cinemachine; 

[System.Serializable]
public struct MeteorData
{
    public Vector3 startPosition;    // 隕石の出現開始座標
    public Vector3 endPosition;      // 隕石の着弾目標座標
    public float delay;              // 演出開始からの落下開始までの遅延時間 (時間差)
}

public class DracoMeteorManager : MonoBehaviour
{
    [Header("流星群プレハブ")]
    public GameObject meteorPrefab;
    public GameObject explosionPrefab;
    public GameObject flashPrefab;    // 予兆の閃光エフェクト
    public GameObject bigFirePrefab;  // 着弾後の大炎上エフェクト

    [Header("流星群設定")]
    public float fallDuration = 1.0f;
    public float explosionDelay = 0f;
    public float explosionDuration = 3f;

    [Header("カメラ設定")]
    public CinemachineCamera defaultCamera;
    public CinemachineCamera followCamera;   // 追跡カメラ (Cinemachine Camera)
    public CinemachineCamera targetCamera;   // 着弾点カメラ (Cinemachine Camera)
    [Range(0.1f, 1f)]
    public float switchTimeRatio = 0.8f;      // カメラ切り替えタイミングの割合
    public CinemachineImpulseSource impulseSource; // 着弾時の振動源
    public float continuousImpulseStrength = 0.5f; // 追跡中の連続振動の強さ
    public float continuousImpulseInterval = 0.1f; // 連続振動の間隔

    [Header("環境設定")]
    public Material defaultSkyboxMaterial; // ★【追加】デフォルトの空マテリアル★
    public Material eventSkyboxMaterial;   // ★【追加】イベント用の赤い空マテリアル★
    public float skyboxExposureEnd = 0.5f; // イベント時の暗い露光度
    public float skyboxFadeDuration = 2.0f; // 空の色変化にかける時間

    [Header("固定落下データ")]
    public MeteorData[] fixedMeteorData;

    public Action OnCompleted;
    
    // シーン開始時に自動実行するためのデバッグ用Start関数
    void Awake()
    {
        // 演出開始前にデフォルトの空を設定
        if (defaultSkyboxMaterial != null)
        {
            RenderSettings.skybox = defaultSkyboxMaterial;
            // 露光度をデフォルトの1.0に戻す（Procedural Skybox前提）
            if (RenderSettings.skybox.HasProperty("_Exposure"))
            {
                 RenderSettings.skybox.SetFloat("_Exposure", 1.0f);
            }
        }
        DynamicGI.UpdateEnvironment();

        // ★追加: 通常カメラを最優先 (30)、演出カメラを低優先 (10) に設定★
        if (defaultCamera != null) defaultCamera.Priority = 30; // 通常は最優先
        if (followCamera != null) followCamera.Priority = 10;
        if (targetCamera != null) targetCamera.Priority = 10;
    
    }

    // 隕石演出の開始トリガー
    public void StartMeteorShower(Action callback)
    {
        OnCompleted = callback;
        StartCoroutine(MeteorShower());
    }

    public IEnumerator MeteorShower()
    {
        Debug.Log("Draco Meteor 演出開始");

        // ★空の色変化 (デフォルト -> イベントマテリアルへの切り替えと露出のフェード)
        yield return StartCoroutine(FadeSkyboxChange(defaultSkyboxMaterial, eventSkyboxMaterial, 1.0f, skyboxExposureEnd, skyboxFadeDuration));

        // ★修正: 演出開始時、通常カメラを低優先 (10) に下げる★
        if (defaultCamera != null) defaultCamera.Priority = 10;
        if (targetCamera != null) targetCamera.Priority = 20; // TargetCameraに制御を渡す 
        if (followCamera != null) followCamera.Priority = 10;


        
        for (int i = 0; i < fixedMeteorData.Length; i++)
        {
            MeteorData data = fixedMeteorData[i];
            StartCoroutine(StartMeteorWithDelay(data));
        }
        
        if (fixedMeteorData.Length == 0)
        {
            Debug.LogWarning("固定落下データが設定されていません。");
            yield return StartCoroutine(FadeSkyboxChange(eventSkyboxMaterial, defaultSkyboxMaterial, skyboxExposureEnd, 1.0f, skyboxFadeDuration));
            OnCompleted?.Invoke();
            yield break;
        }

        // 演出全体の待機
        float maxEndTime = 0f;
        foreach (var data in fixedMeteorData)
        {
            float currentEndTime = data.delay + fallDuration + explosionDelay + explosionDuration;
            if (currentEndTime > maxEndTime)
            {
                maxEndTime = currentEndTime;
            }
        }
        yield return new WaitForSeconds(maxEndTime);

        // ★空の色変化 (イベントマテリアル -> デフォルトマテリアルへの切り替えと露出のフェード)
        yield return StartCoroutine(FadeSkyboxChange(eventSkyboxMaterial, defaultSkyboxMaterial, skyboxExposureEnd, 1.0f, skyboxFadeDuration));


        if (targetCamera != null) targetCamera.Priority = 10;
        if (followCamera != null) followCamera.Priority = 10;
        if (defaultCamera != null) defaultCamera.Priority = 30; // 通常カメラに制御を戻す
    
        Debug.Log("--- Draco Meteor 演出完了 ---");
        OnCompleted?.Invoke();
    }
    
    private IEnumerator StartMeteorWithDelay(MeteorData data)
    {
        yield return new WaitForSeconds(data.delay);
        StartCoroutine(AnimateMeteor(data.startPosition, data.endPosition));
    }

    private IEnumerator AnimateMeteor(Vector3 startPos, Vector3 endPos)
    {
        if (meteorPrefab == null) yield break;
        GameObject meteor = Instantiate(meteorPrefab, startPos, Quaternion.identity);

        // カメラ追跡設定
        if (followCamera != null)
        {
            followCamera.Follow = meteor.transform;
            followCamera.LookAt = meteor.transform;
            followCamera.Priority = 30; // 追跡開始
            if (targetCamera != null) targetCamera.Priority = 20;
        }

        // Trail Rendererと回転の設定
        TrailRenderer trail = meteor.GetComponentInChildren<TrailRenderer>();
        if (trail != null) trail.emitting = true;
        Vector3 direction = (endPos - startPos).normalized;
        meteor.transform.rotation = Quaternion.LookRotation(direction);
        
        float timer = 0f;
        float switchTime = fallDuration * switchTimeRatio;
        float impulseTimer = 0f; // 連続振動用タイマー

        // 落下アニメーション
        while (timer < fallDuration)
        {
            // ★追跡中のカメラ振動
            if (impulseSource != null && timer < fallDuration) 
            {
                if (impulseTimer >= continuousImpulseInterval)
                {
                    impulseSource.GenerateImpulse(continuousImpulseStrength); 
                    impulseTimer = 0f;
                }
                impulseTimer += Time.deltaTime;
            }

            // ★カメラ切り替えのタイミング
            if (timer >= switchTime && followCamera != null)
            {
                // ★予兆の閃光
                if (flashPrefab != null)
                {
                    GameObject flash = Instantiate(flashPrefab, endPos, Quaternion.identity);
                    Destroy(flash, 0.15f); // 0.15秒で消滅
                }

                // カメラ切り替え実行
                followCamera.Priority = 10; 
                if (targetCamera != null) targetCamera.Priority = 30; 
            }

            meteor.transform.position = Vector3.Lerp(startPos, endPos, timer / fallDuration);
            timer += Time.deltaTime;
            yield return null;
        }

        meteor.transform.position = endPos; 
        
        // ★着弾時振動（最も強い振動）
        if (impulseSource != null)
        {
            impulseSource.GenerateImpulse(1.0f); // 強い振動を発生
        }
        
        // Trail Rendererの描画停止と待機
        if (trail != null)
        {
            trail.emitting = false;
            yield return new WaitForSeconds(trail.time); 
        }

        // 爆発と大炎上の生成
        yield return new WaitForSeconds(explosionDelay);
        
        // ★大炎上の生成
        if (bigFirePrefab != null)
        {
            GameObject fire = Instantiate(bigFirePrefab, endPos, Quaternion.identity);
            Destroy(fire, 8.0f); 
        }

        if (explosionPrefab != null)
        {
            GameObject explosion = Instantiate(explosionPrefab, endPos, Quaternion.identity);
            Destroy(explosion, explosionDuration);
        }

        Destroy(meteor);
    }
    
    // ★【修正】マテリアルの切り替えと露出をフェードさせるコルーチン
    private IEnumerator FadeSkyboxChange(Material startMat, Material endMat, float startExp, float endExp, float duration)
    {
        float timer = 0f;
        
        // 開始マテリアルが既に設定されていることを確認
        if (RenderSettings.skybox != startMat)
        {
            RenderSettings.skybox = startMat;
        }
        
        // 露出度の初期設定（念のため）
        if (startMat != null && startMat.HasProperty("_Exposure"))
        {
             RenderSettings.skybox.SetFloat("_Exposure", startExp);
        }
        
        // フェード開始
        while (timer < duration)
        {
            float t = timer / duration;
            
            // 演出中はマテリアルを切り替えるためにブレンドは行わず、露出度のみを滑らかに変化させる
            if (endMat != null && endMat.HasProperty("_Exposure"))
            {
                 // マテリアルを最終形に切り替え、その露出度を操作することでフェードを表現
                 RenderSettings.skybox = endMat;
                 RenderSettings.skybox.SetFloat("_Exposure", Mathf.Lerp(startExp, endExp, t));
            }
            
            timer += Time.deltaTime;
            yield return null;
        }
        
        // 最終的なマテリアルと露出度を設定
        if (endMat != null) RenderSettings.skybox = endMat;
        if (RenderSettings.skybox != null && RenderSettings.skybox.HasProperty("_Exposure"))
        {
             RenderSettings.skybox.SetFloat("_Exposure", endExp);
        }
        
        // 環境光の更新
        DynamicGI.UpdateEnvironment();
    }
}