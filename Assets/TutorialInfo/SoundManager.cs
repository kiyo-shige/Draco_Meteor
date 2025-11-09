using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// BGMの状態を定義
public enum BGMState 
{
    NONE,                   // BGMは流れていない
    SPINNING_BGM,           // 回転スタート時/回転中BGM
    REACH_BGM,              // リーチ演出時BGM
    REACH_MISS_BGM,         // リーチ外れ時BGM
    METEOR_BGM,             // 隕石落下シーンのBGM
    DESCENDING_NUMBER_BGM,  // 数字降下演出のBGM
    FINAL_SPINNING_BGM,     // 最後の回転中/減速中BGM
    JACKPOT_FANFARE         // 最後の点滅/大当たりファンファーレ
}

// BGMの設定をInspectorで調整可能にする構造体
[System.Serializable]
public class BGMClipSetting
{
    public AudioClip clip;
    [Tooltip("このBGMを再生する際の最大音量")]
    [Range(0f, 1f)]
    public float volume = 1.0f;
    [Tooltip("BGMがループするかどうか")]
    public bool loop = true;
    [Tooltip("BGMをフェードイン/アウトさせる時間 (秒)")]
    public float fadeTime = 0.5f; 
}


public class SoundManager : MonoBehaviour
{
    // 静的インスタンス（シングルトン）
    public static SoundManager Instance { get; private set; }

    [Header("Audio Source")]
    [SerializeField] private AudioSource seAudioSource; // SE再生用 (PlayOneShot)
    [SerializeField] private AudioSource bgmAudioSource; // BGM再生用 (Loop/Stop制御)

    [Header("BGM状態")]
    private BGMState currentBGMState = BGMState.NONE; 
    
    // --- SE Clips ---
    [Header("SE Audio Clips")]
    public AudioClip startSpinClip;        // 回転スタート効果音
    public AudioClip stopReelClip;        
    public AudioClip reachFlashClip;       
    public AudioClip meteorDropClip;       // 隕石落下のSE
    public AudioClip meteorExplosionClip;  // 隕石落下時の爆発音
    public AudioClip descendingNumberSE;   // 数字降下演出の効果音

    // --- BGM Clips (設定制御) ---
    [Header("BGM/Fanfare Audio Clips")]
    public BGMClipSetting spinningBgmSetting;        
    public BGMClipSetting reachBgmSetting;           
    public BGMClipSetting reachMissBgmSetting;       // リーチ外れ時BGM
    public BGMClipSetting meteorBgmSetting;          
    public BGMClipSetting descendingNumberSetting;   
    public BGMClipSetting finalSpinningBgmSetting;   
    public BGMClipSetting jackpotFanfareSetting;     

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
        
        // エディター設定漏れの警告
        if (seAudioSource == null || bgmAudioSource == null)
        {
            Debug.LogError("SoundManager: Audio Sourceが不足しています。SE用とBGM用の2つを設定してください。");
        }
    }

    // --- SE 再生 ---
    public void PlaySE(AudioClip clip)
    {
        if (seAudioSource != null && clip != null)
        {
            // PlayOneShot: SEを重ねて再生する標準的な方法
            seAudioSource.PlayOneShot(clip); 
        }
    }
    
    // SEラッパー
    public void PlayStartSpinSE() => PlaySE(startSpinClip);
    public void PlayStopReelSE() => PlaySE(stopReelClip);
    public void PlayReachFlashSE() => PlaySE(reachFlashClip);
    public void PlayMeteorDropSE() => PlaySE(meteorDropClip);
    public void PlayMeteorExplosionSE() => PlaySE(meteorExplosionClip);
    public void PlayDescendingNumberSE() => PlaySE(descendingNumberSE);


    // --- BGM 再生/停止 ---
    private BGMClipSetting GetBGMSettingByState(BGMState state)
    {
        if (state == BGMState.SPINNING_BGM) return spinningBgmSetting;
        if (state == BGMState.REACH_BGM) return reachBgmSetting;
        if (state == BGMState.REACH_MISS_BGM) return reachMissBgmSetting;
        if (state == BGMState.METEOR_BGM) return meteorBgmSetting;
        if (state == BGMState.DESCENDING_NUMBER_BGM) return descendingNumberSetting;
        if (state == BGMState.FINAL_SPINNING_BGM) return finalSpinningBgmSetting;
        if (state == BGMState.JACKPOT_FANFARE) return jackpotFanfareSetting;
        return new BGMClipSetting { clip = null, volume = 0f, fadeTime = 0.5f }; 
    }

    /// <summary>BGMを再生し、状態を更新する (既存のBGMは即時停止して新しいBGMを開始)</summary>
    public void PlayBGM(BGMClipSetting setting, BGMState newState)
    {
        if (bgmAudioSource == null || setting.clip == null) return;

        // 既に再生中のBGMがある場合、即座に停止
        if (bgmAudioSource.isPlaying)
        {
            bgmAudioSource.Stop();
        }
        
        bgmAudioSource.clip = setting.clip;
        bgmAudioSource.loop = setting.loop;
        bgmAudioSource.volume = 0f;
        bgmAudioSource.Play();

        // 状態を更新
        currentBGMState = newState; 

        // フェードインを開始
        StartCoroutine(FadeVolume(bgmAudioSource, setting.volume, setting.fadeTime));
    }


    /// <summary>BGMの再生を停止し、現在の状態フラグに定義されたフェードアウトを適用する</summary>
    public void StopBGM()
    {
        if (currentBGMState == BGMState.NONE || bgmAudioSource == null || !bgmAudioSource.isPlaying) return; 

        // 現在の状態からフェード時間を取得
        float fadeTime = GetBGMSettingByState(currentBGMState).fadeTime;
        
        // フェードアウトを開始。完了後、Stop()を実行
        StartCoroutine(FadeVolume(bgmAudioSource, 0f, fadeTime, true));

        // 状態をリセット
        currentBGMState = BGMState.NONE; 
    }
    
    // BGMラッパーメソッド
    public void PlaySpinningBGM() => PlayBGM(spinningBgmSetting, BGMState.SPINNING_BGM);
    public void PlayReachBGM() => PlayBGM(reachBgmSetting, BGMState.REACH_BGM);
    public void PlayReachMissBGM() => PlayBGM(reachMissBgmSetting, BGMState.REACH_MISS_BGM);
    public void PlayMeteorBGM() => PlayBGM(meteorBgmSetting, BGMState.METEOR_BGM); // ★追加/確認★
    public void PlayDescendingNumberBGM() => PlayBGM(descendingNumberSetting, BGMState.DESCENDING_NUMBER_BGM);
    public void PlayFinalSpinningBGM() => PlayBGM(finalSpinningBgmSetting, BGMState.FINAL_SPINNING_BGM);
    public void PlayJackpotFanfareBGM() => PlayBGM(jackpotFanfareSetting, BGMState.JACKPOT_FANFARE);


    /// <summary>指定した時間でオーディオソースの音量を目標値へ変化させるコルーチン</summary>
    private IEnumerator FadeVolume(AudioSource source, float targetVolume, float duration, bool shouldStop = false)
    {
        float startVolume = source.volume;
        float startTime = Time.time;
        
        if (duration <= 0) 
        {
            source.volume = targetVolume;
            if (shouldStop) source.Stop();
            yield break;
        }

        while (Time.time < startTime + duration)
        {
            float t = (Time.time - startTime) / duration;
            source.volume = Mathf.Lerp(startVolume, targetVolume, t);
            yield return null;
        }

        source.volume = targetVolume; 

        if (shouldStop)
        {
            source.Stop();
            source.clip = null;
        }
    }
}