using UnityEngine;
using System.Collections.Generic;
using UnityEditor.Animations; // Required for enums

[CreateAssetMenu(fileName = "NewAnimatorPreset", menuName = "AI Assistant/Animator/Animator State Preset", order = 1)]
public class AnimatorPreset : ScriptableObject
{
    public string presetName = "New Preset";

    [Tooltip("A key to identify this preset, e.g., 'Warrior', 'Mage', 'Default'.")]
    public string characterClassKey = "Default";

    public List<ParameterDefinition> parameters;
    public List<StateDefinition> states;
    public List<TransitionDefinition> transitions;
}

[System.Serializable]
public class ParameterDefinition
{
    public string name;
    public AnimatorControllerParameterType type = AnimatorControllerParameterType.Trigger;
}

[System.Serializable]
public class StateDefinition
{
    public string name;
    public AnimationClip animationClip; // <-- This line is required.
    public bool isDefaultState;
}

[System.Serializable]
public class TransitionDefinition
{
    public string sourceState;
    public string destinationState;

    [Tooltip("If checked, the transition will wait for the source animation to finish.")]
    public bool hasExitTime = true;

    [Tooltip("How long the transition should take (in seconds).")]
    public float duration = 0.25f;

    public List<ConditionDefinition> conditions;
}

[System.Serializable]
public class ConditionDefinition
{
    public string parameterName;
    public AnimatorConditionMode mode = AnimatorConditionMode.If;
    public float threshold;
}