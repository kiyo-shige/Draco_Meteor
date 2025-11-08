using UnityEngine;
using System.Collections;
using System;

public class ReelController : MonoBehaviour
{
    // === 設定可能なパラメータ ===
    [Header("回転設定")]
    public float initialSpeed = 360f;      // 最初の速度 (度/秒)
    public float secondSpeed = 120f;
    public float thirdSpeed = 45f;
    public float forthSpeed = 18f;
    public float stopDuration = 0.75f; //停止にかける時間
    public int minimumRevolutions = 1; //最低回転回数

    [Header("回転軸 (X軸)")]
    public Vector3 rotationAxis = Vector3.right;


    // === 内部状態 ===
    private bool isSpinning = false;
    private float currentSpeed;          // 回転を開始した時刻
    private float accumulatedRotation = 0f;       //総回転量
    private float targetAngle;

    public bool IsSpinning => isSpinning;
    public Action OnStopCompleted;

    /**
    --------------------------------------
    public methods
    --------------------------------------
    **/


    // 外部からスピンを開始するメソッド
    public void StartSpin(float finalAngle)
    {
        if (isSpinning) return;
        ResetReel();
        targetAngle = finalAngle;
        currentSpeed = initialSpeed;
        StartCoroutine(SpinOnly());
    }

    // 外部から減速を開始するメソッド
    public void StartDeceleration(float firstStopOffset = 180f)
    {
        if (!isSpinning) return;

        StopAllCoroutines();
        StartCoroutine(DecelerationAndStopSequence(firstStopOffset));
    }

    // リセット機能
    public void ResetReel()
    {
        if (isSpinning) StopAllCoroutines();

        isSpinning = false;
        currentSpeed = 0f;
        accumulatedRotation = 0f;
        transform.localEulerAngles = Vector3.zero; // 角度を0にリセット
    }

    /**
   --------------------------------------
   Coroutine
   --------------------------------------
   **/

    //等速回転機能
    private IEnumerator SpinOnly()
    {
        isSpinning = true;

        while (true)
        {
            float rotationAmount = currentSpeed * Time.deltaTime;
            transform.Rotate(rotationAxis, rotationAmount, Space.Self);
            accumulatedRotation += rotationAmount;
            yield return null;
        }
    }

    private IEnumerator DecelerationAndStopSequence(float firstStopOffset)
    {
        float startRotation = accumulatedRotation;

        float requiredRevolutions = Mathf.Ceil((startRotation + (minimumRevolutions * 360f) - targetAngle) / 360f);
        float absoluteTargetAngle = targetAngle + requiredRevolutions * 360f;

        // 1. initialSpeedからsecondSpeedへ減速

        // 1.1 secondSpeed
        currentSpeed = secondSpeed;
        float DEG_SECOND_TRANSITION = firstStopOffset; //目標のfirstStopOffset手前まで
        float secondTarget = absoluteTargetAngle - DEG_SECOND_TRANSITION;

        //　すでに目標を過ぎていたらもう一周
        while (secondTarget < accumulatedRotation)
        {
            secondTarget += 360f;
        }
        
        while (accumulatedRotation < secondTarget)
        {
            float rotationAmount = currentSpeed * Time.deltaTime;
            transform.Rotate(rotationAxis, rotationAmount, Space.Self);
            accumulatedRotation += rotationAmount;
            yield return null;
        }

        // 1.2 thirdSpeed
        currentSpeed = thirdSpeed;
        const float DEG_THIRD_TRANSITION = 3.0f; //目標の30度手前まで
        float thirdTarget = absoluteTargetAngle - DEG_THIRD_TRANSITION;

        while (accumulatedRotation < thirdTarget)
        {
            float rotationAmount = currentSpeed * Time.deltaTime;
            transform.Rotate(rotationAxis, rotationAmount, Space.Self);
            accumulatedRotation += rotationAmount;
            yield return null;
        }
        
        // 1.3 forthSpeed
        currentSpeed = forthSpeed;
        const float DEG_FORTH_TRANSITION = 0.1f; //目標の0.1度手前まで
        float forthTarget = absoluteTargetAngle - DEG_FORTH_TRANSITION;

        while (accumulatedRotation < forthTarget)
        {
            float rotationAmount = currentSpeed * Time.deltaTime;
            transform.Rotate(rotationAxis, rotationAmount, Space.Self);
            accumulatedRotation += rotationAmount;
            yield return null;
        }

        // 2. 補間処理 (Lerp) 
        float lerpStartRotation = accumulatedRotation;
        float t = 0;

        while (t < 1)
        {
            t += Time.deltaTime / stopDuration;
            if (t > 1) t = 1;

            float easedT = Mathf.SmoothStep(0f, 1f, t);
            float newAbsoluteRotation = Mathf.Lerp(lerpStartRotation, absoluteTargetAngle, easedT);

            accumulatedRotation = newAbsoluteRotation;
            transform.localEulerAngles = new Vector3(newAbsoluteRotation % 360f, 0f, 0f);

            yield return null;
        }
        
        // --- 最終停止位置のロック ---
        transform.localEulerAngles = new Vector3(targetAngle, 0f, 0f);
        
        isSpinning = false;
        Debug.Log($"リール停止完了。最終角度：{targetAngle}");

        // 停止完了を通知
        OnStopCompleted?.Invoke();

    }

}