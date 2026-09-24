using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Bola;

// Experimental upper-body retargeter, explicitly driven by the prototype fixture.
[DefaultExecutionOrder(30000)]
public sealed class PoseOverlay : MonoBehaviour
{
    private Animator visible=null!, hidden=null!;
    private GameObject rig=null!;
    private PlayableGraph graph;
    private AnimationClipPlayable playable;
    private readonly List<(Transform Visible,Transform Hidden)> bones=new();
    private Quaternion[]? transitionPose;
    private float transitionTime,transitionDuration;
    private Quaternion[][]? baked;
    private Quaternion[]? bakedSpine;
    private float bakedLength;
    public bool UseBakedCurves { get; set; }
    public float ClipTime { get; set; }
    public float Blend { get; set; }
    public Vector3 SampledHand => hidden.GetBoneTransform(HumanBodyBones.RightHand).position;
    public bool CompensateHips { get; set; } = true;
    public void SetClip(AnimationClip clip,float blendSeconds=.12f)
    {
        transitionPose=new Quaternion[bones.Count];
        for(int i=0;i<bones.Count;i++) transitionPose[i]=bones[i].Visible.localRotation;
        transitionTime=Time.time; transitionDuration=Mathf.Max(.001f,blendSeconds);
        var replacement=AnimationClipPlayable.Create(graph,clip);
        replacement.SetApplyFootIK(false); replacement.SetApplyPlayableIK(false);
        var output=(AnimationPlayableOutput)graph.GetOutput(0);
        output.SetSourcePlayable(replacement);
        playable.Destroy(); playable=replacement; ClipTime=0;
        baked=null; bakedSpine=null;
    }
    // Prototype fallback: bake mapped transforms on this player's avatar, then apply
    // the sampled upper-body curves without evaluating a hidden animator each frame.
    public void Bake(AnimationClip clip)
    {
        SetClip(clip,.001f);
        bakedLength=clip.length;
        int frames=Mathf.CeilToInt(clip.length*60)+1;
        baked=new Quaternion[frames][]; bakedSpine=new Quaternion[frames];
        for(int frame=0;frame<frames;frame++)
        {
            playable.SetTime(clip.length*frame/(frames-1)); graph.Evaluate(0);
            baked[frame]=new Quaternion[bones.Count];
            for(int i=0;i<bones.Count;i++) baked[frame][i]=bones[i].Hidden.localRotation;
            bakedSpine[frame]=Quaternion.Inverse(rig.transform.rotation)*hidden.GetBoneTransform(HumanBodyBones.Spine).rotation;
        }
        UseBakedCurves=true;
    }
    public void Initialize(Animator animator,AnimationClip clip)
    {
        if(!animator.avatar || !animator.avatar.isHuman || !animator.avatar.isValid) throw new InvalidOperationException("Player humanoid avatar unavailable");
        visible=animator;
        rig=Copy(animator.transform,null).gameObject; rig.name="BolaAnimationOnlyRig";
        hidden=rig.AddComponent<Animator>(); hidden.avatar=animator.avatar;
        hidden.applyRootMotion=false; hidden.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        graph=PlayableGraph.Create("BolaPrototypePose"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        playable=AnimationClipPlayable.Create(graph,clip); playable.SetApplyFootIK(false); playable.SetApplyPlayableIK(false);
        AnimationPlayableOutput.Create(graph,"Pose",hidden).SetSourcePlayable(playable); graph.Play(); graph.Evaluate(0);
        foreach(HumanBodyBones bone in Enum.GetValues(typeof(HumanBodyBones)))
        {
            if(bone==HumanBodyBones.LastBone || bone==HumanBodyBones.Hips || bone==HumanBodyBones.LeftUpperLeg || bone==HumanBodyBones.RightUpperLeg ||
               bone==HumanBodyBones.LeftLowerLeg || bone==HumanBodyBones.RightLowerLeg || bone==HumanBodyBones.LeftFoot || bone==HumanBodyBones.RightFoot ||
               bone==HumanBodyBones.LeftToes || bone==HumanBodyBones.RightToes) continue;
            var v=visible.GetBoneTransform(bone); var h=hidden.GetBoneTransform(bone);
            if(v&&h) bones.Add((v,h));
        }
    }
    private static Transform Copy(Transform source,Transform? parent)
    {
        var target=new GameObject(source.name).transform;
        target.SetParent(parent,false); target.localPosition=source.localPosition;
        target.localRotation=source.localRotation; target.localScale=source.localScale;
        foreach(Transform child in source) Copy(child,target);
        return target;
    }
    public void Sample()
    {
        if(!graph.IsValid()) return;
        rig.transform.SetPositionAndRotation(visible.transform.position,visible.transform.rotation);
        Quaternion desiredSpine;
        if(UseBakedCurves && baked!=null && bakedSpine!=null)
        {
            float frame=Mathf.Clamp01(ClipTime/bakedLength)*(baked.Length-1);
            int first=Mathf.FloorToInt(frame),second=Mathf.Min(first+1,baked.Length-1);
            for(int i=0;i<bones.Count;i++) bones[i].Visible.localRotation=Quaternion.Slerp(bones[i].Visible.localRotation,Quaternion.Slerp(baked[first][i],baked[second][i],frame-first),Blend);
            desiredSpine=rig.transform.rotation*Quaternion.Slerp(bakedSpine[first],bakedSpine[second],frame-first);
        }
        else
        {
            playable.SetTime(ClipTime); playable.SetSpeed(0); graph.Evaluate(0);
            foreach(var pair in bones) pair.Visible.localRotation=Quaternion.Slerp(pair.Visible.localRotation,pair.Hidden.localRotation,Blend);
            desiredSpine=hidden.GetBoneTransform(HumanBodyBones.Spine).rotation;
        }
        // Root-locked clips can still rotate the humanoid hips. Preserve that contribution
        // at the spine boundary while leaving the visible pelvis and legs under vanilla control.
        if(CompensateHips)
        {
            var spine=visible.GetBoneTransform(HumanBodyBones.Spine);
            spine.rotation=Quaternion.Slerp(spine.rotation,desiredSpine,Blend);
        }
        if(transitionPose!=null)
        {
            float t=Mathf.SmoothStep(0,1,(Time.time-transitionTime)/transitionDuration);
            for(int i=0;i<bones.Count;i++) bones[i].Visible.localRotation=Quaternion.Slerp(transitionPose[i],bones[i].Visible.localRotation,t);
            if(t>=1) transitionPose=null;
        }
    }
    private void LateUpdate() => Sample();
    private void OnDestroy() { if(graph.IsValid()) graph.Destroy(); if(rig) Destroy(rig); }
}
