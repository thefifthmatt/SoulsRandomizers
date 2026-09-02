RegisterTableGoal(GOAL_DarknessBigBrother_525000_Battle, "DarknessBigBrotherBattle")
REGISTER_GOAL_NO_SUB_GOAL(GOAL_DarknessBigBrother_525000_Battle, true)

Goal.Initialize = function (self, ai, goal, battleActivatedCount)
    ai:SetNumber(1, 2)
    ai:SetNumber(2, 0)
    ai:SetNumber(3, 0)
    
end

Goal.Activate = function (self, ai, goal)
    Init_Pseudo_Global(ai, goal)
    local probabilities = {}
    local acts = {}
    local paramTbls = {}
    Common_Clear_Param(probabilities, acts, paramTbls)
    local distanceEnemy = ai:GetDist(TARGET_ENE_0)
    local paramDoAdmire = ai:GetExcelParam(AI_EXCEL_THINK_PARAM_TYPE__thinkAttr_doAdmirer)
    if ai:IsInsideTarget(TARGET_ENE_0, AI_DIR_TYPE_B, 180) then
        if ai:IsInsideTarget(TARGET_ENE_0, AI_DIR_TYPE_B, 60) and distanceEnemy <= 999 then
            probabilities[13] = 80
            probabilities[20] = 20
        elseif ai:IsInsideTarget(TARGET_ENE_0, AI_DIR_TYPE_L, 180) and distanceEnemy <= 999 then
            probabilities[12] = 80
            probabilities[20] = 20
        elseif ai:IsInsideTarget(TARGET_ENE_0, AI_DIR_TYPE_R, 180) and distanceEnemy <= 999 then
            probabilities[11] = 80
            probabilities[20] = 20
        else
            probabilities[20] = 100
        end
    elseif distanceEnemy >= 8 then
        probabilities[1] = 0
        probabilities[2] = 0
        probabilities[3] = 0
        probabilities[4] = 0
        probabilities[5] = 10
        probabilities[6] = 20
        probabilities[7] = 30
        probabilities[8] = 5
        probabilities[9] = 5
        probabilities[10] = 0
        probabilities[14] = 0
        probabilities[15] = 20
        probabilities[16] = 20
        probabilities[21] = 0
    elseif distanceEnemy >= 5 then
        probabilities[1] = 0
        probabilities[2] = 0
        probabilities[3] = 10
        probabilities[4] = 10
        probabilities[5] = 20
        probabilities[6] = 20
        probabilities[7] = 0
        probabilities[8] = 0
        probabilities[9] = 0
        probabilities[10] = 0
        probabilities[14] = 0
        probabilities[15] = 10
        probabilities[16] = 20
        probabilities[21] = 20
    else
        probabilities[1] = 25
        probabilities[2] = 25
        probabilities[3] = 0
        probabilities[4] = 0
        probabilities[5] = 0
        probabilities[6] = 0
        probabilities[7] = 0
        probabilities[8] = 10
        probabilities[9] = 10
        probabilities[10] = 20
        probabilities[14] = 10
        probabilities[15] = 0
        probabilities[16] = 0
        probabilities[21] = 0
    end
    if ai:GetNpcThinkParamID() == 525000 then
        if ai:GetNumber(2) == 0 then
            goal:AddSubGoal(GOAL_COMMON_Wait, 1, TARGET_NONE, 0, 0, 0)
            probabilities[1] = 0
            probabilities[2] = 0
            probabilities[3] = 0
            probabilities[4] = 0
            probabilities[5] = 0
            probabilities[6] = 0
            probabilities[7] = 0
            probabilities[8] = 0
            probabilities[9] = 100
            probabilities[10] = 0
            probabilities[11] = 0
            probabilities[12] = 0
            probabilities[13] = 0
            probabilities[14] = 0
            probabilities[15] = 0
            probabilities[16] = 0
            probabilities[20] = 0
            probabilities[21] = 0
            probabilities[22] = 0
            probabilities[23] = 0
        end
        probabilities[14] = 0
        probabilities[15] = 0
        probabilities[16] = 0
    end
    if ai:GetNpcThinkParamID() == 525001 then
        if ai:GetNumber(3) == 0 then
            ai:SetNumber(3, 1)
            probabilities[1] = 0
            probabilities[2] = 0
            probabilities[3] = 0
            probabilities[4] = 0
            probabilities[5] = 0
            probabilities[6] = 0
            probabilities[7] = 0
            probabilities[8] = 0
            probabilities[9] = 0
            probabilities[10] = 0
            probabilities[11] = 0
            probabilities[12] = 0
            probabilities[13] = 0
            probabilities[14] = 0
            probabilities[15] = 100
            probabilities[16] = 0
            probabilities[20] = 0
            probabilities[21] = 0
            probabilities[22] = 0
            probabilities[23] = 0
        end
        if ai:GetHpRate(TARGET_SELF) <= 0.01 then
            probabilities[1] = 0
            probabilities[2] = 0
            probabilities[3] = 0
            probabilities[4] = 0
            probabilities[5] = 0
            probabilities[6] = 0
            probabilities[7] = 0
            probabilities[8] = 0
            probabilities[9] = 0
            probabilities[10] = 0
            probabilities[11] = 0
            probabilities[12] = 0
            probabilities[13] = 0
            probabilities[14] = 0
            probabilities[15] = 0
            probabilities[16] = 0
            probabilities[20] = 0
            probabilities[21] = 0
            probabilities[22] = 0
            probabilities[23] = 100
        end
    end
    probabilities[1] = SetCoolTime(ai, goal, 3000, 9, probabilities[1], 1)
    probabilities[2] = SetCoolTime(ai, goal, 3011, 9, probabilities[2], 1)
    probabilities[3] = SetCoolTime(ai, goal, 3005, 18, probabilities[3], 1)
    probabilities[3] = SetCoolTime(ai, goal, 3032, 18, probabilities[3], 1)
    probabilities[4] = SetCoolTime(ai, goal, 3006, 9, probabilities[4], 1)
    probabilities[5] = SetCoolTime(ai, goal, 3007, 9, probabilities[5], 1)
    probabilities[6] = SetCoolTime(ai, goal, 3013, 18, probabilities[6], 1)
    probabilities[7] = SetCoolTime(ai, goal, 3035, 9, probabilities[7], 1)
    probabilities[8] = SetCoolTime(ai, goal, 3018, 20, probabilities[8], 1)
    probabilities[8] = SetCoolTime(ai, goal, 3033, 20, probabilities[8], 1)
    probabilities[9] = SetCoolTime(ai, goal, 3003, 20, probabilities[9], 1)
    probabilities[9] = SetCoolTime(ai, goal, 3004, 20, probabilities[9], 1)
    probabilities[10] = SetCoolTime(ai, goal, 3022, 30, probabilities[10], 1)
    probabilities[10] = SetCoolTime(ai, goal, 3036, 30, probabilities[10], 1)
    probabilities[11] = SetCoolTime(ai, goal, 3008, 9, probabilities[11], 1)
    probabilities[12] = SetCoolTime(ai, goal, 3009, 9, probabilities[12], 1)
    probabilities[13] = SetCoolTime(ai, goal, 3019, 9, probabilities[13], 1)
    probabilities[14] = SetCoolTime(ai, goal, 3015, 20, probabilities[14], 1)
    probabilities[15] = SetCoolTime(ai, goal, 3023, 20, probabilities[15], 1)
    probabilities[16] = SetCoolTime(ai, goal, 3034, 9, probabilities[16], 1)
    if ai:IsInsideMsbRegion(TARGET_ENE_0, AI_DIR_TYPE_F, 0, 3412860) == true then
        probabilities[1] = 0
        probabilities[2] = 0
        probabilities[3] = 20
        probabilities[4] = 20
        probabilities[5] = 20
        probabilities[6] = 20
        probabilities[7] = 20
        probabilities[8] = 0
        probabilities[9] = 0
        probabilities[10] = 0
        probabilities[11] = 0
        probabilities[12] = 0
        probabilities[13] = 0
        probabilities[14] = 0
        probabilities[15] = 0
        probabilities[16] = 0
        probabilities[20] = 0
        probabilities[21] = 0
        probabilities[22] = 0
        probabilities[23] = 0
    end
    if ai:IsInsideMsbRegion(TARGET_SELF, AI_DIR_TYPE_F, 0, 3412860) == true then
        probabilities[1] = 0
        probabilities[2] = 0
        probabilities[3] = 0
        probabilities[4] = 0
        probabilities[5] = 0
        probabilities[6] = 0
        probabilities[7] = 0
        probabilities[8] = 0
        probabilities[9] = 0
        probabilities[10] = 0
        probabilities[11] = 0
        probabilities[12] = 0
        probabilities[13] = 0
        probabilities[14] = 100
        probabilities[15] = 0
        probabilities[16] = 0
        probabilities[20] = 0
        probabilities[21] = 0
        probabilities[22] = 0
        probabilities[23] = 0
    end
    acts[1] = REGIST_FUNC(ai, goal, DarknessBigBrother_Act01)
    acts[2] = REGIST_FUNC(ai, goal, DarknessBigBrother_Act02)
    acts[3] = REGIST_FUNC(ai, goal, DarknessBigBrother_Act03)
    acts[4] = REGIST_FUNC(ai, goal, DarknessBigBrother_Act04)
    acts[5] = REGIST_FUNC(ai, goal, DarknessBigBrother_Act05)
    acts[6] = REGIST_FUNC(ai, goal, DarknessBigBrother_Act06)
    acts[7] = REGIST_FUNC(ai, goal, DarknessBigBrother_Act07)
    acts[8] = REGIST_FUNC(ai, goal, DarknessBigBrother_Act08)
    acts[9] = REGIST_FUNC(ai, goal, DarknessBigBrother_Act09)
    acts[10] = REGIST_FUNC(ai, goal, DarknessBigBrother_Act10)
    acts[11] = REGIST_FUNC(ai, goal, DarknessBigBrother_Act11)
    acts[12] = REGIST_FUNC(ai, goal, DarknessBigBrother_Act12)
    acts[13] = REGIST_FUNC(ai, goal, DarknessBigBrother_Act13)
    acts[14] = REGIST_FUNC(ai, goal, DarknessBigBrother_Act14)
    acts[15] = REGIST_FUNC(ai, goal, DarknessBigBrother_Act15)
    acts[16] = REGIST_FUNC(ai, goal, DarknessBigBrother_Act16)
    acts[20] = REGIST_FUNC(ai, goal, DarknessBigBrother_Act20)
    acts[21] = REGIST_FUNC(ai, goal, DarknessBigBrother_Act21)
    acts[22] = REGIST_FUNC(ai, goal, DarknessBigBrother_Act22)
    acts[23] = REGIST_FUNC(ai, goal, DarknessBigBrother_Act23)
    acts[24] = REGIST_FUNC(ai, goal, DarknessBigBrother_Act24)
    local actAfter = REGIST_FUNC(ai, goal, DarknessBigBrother_ActAfter_AdjustSpace)
    Common_Battle_Activate(ai, goal, probabilities, acts, actAfter, paramTbls)
    
end

function DarknessBigBrother_Act01(ai, goal, paramTbl)
    local stopDist = 8
    local canRunDist = 8
    local forceRunMinDist = 8 + 999
    local runProbability = 0
    local guardProbability = 0
    local walkLife = 2
    local runLife = 2
    Approach_Act_Flex(ai, goal, stopDist, canRunDist, forceRunMinDist, runProbability, guardProbability, walkLife, runLife)
    local animationId = 3000
    local animationId_2 = 3001
    local animationId_3 = 3025
    local animationId_4 = 3010
    local successDistance = 5.96
    local successDistance_2 = 7.11
    local successDistance_3 = 8.42
    local successDistance_4 = 999
    local turnTime = 0
    local turnFaceAngle = 0
    local random = ai:GetRandam_Int(1, 100)
    local distanceEnemy = ai:GetDist(TARGET_ENE_0)
    if random <= 60 then
        goal:AddSubGoal(GOAL_COMMON_ComboAttackTunableSpin, 10, animationId, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle, 0, 0)
        goal:AddSubGoal(GOAL_COMMON_ComboFinal, 10, animationId_2, TARGET_ENE_0, successDistance_4, 0, 0)
    elseif random <= 80 then
        goal:AddSubGoal(GOAL_COMMON_ComboAttackTunableSpin, 10, animationId, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle, 0, 0)
        goal:AddSubGoal(GOAL_COMMON_ComboRepeat, 10, animationId_2, TARGET_ENE_0, successDistance_3, 0, 0)
        goal:AddSubGoal(GOAL_COMMON_ComboRepeat, 10, animationId_4, TARGET_ENE_0, successDistance_4, 0, 0)
    else
        ai:SetNumber(1, 1)
        goal:AddSubGoal(GOAL_COMMON_ComboAttackTunableSpin, 10, animationId, TARGET_ENE_0, successDistance_2, turnTime, turnFaceAngle, 0, 0)
        goal:AddSubGoal(GOAL_COMMON_ComboRepeat_SuccessAngle180, 10, animationId_3, TARGET_ENE_0, 999, turnTime, turnFaceAngle, 0, 180)
        goal:AddSubGoal(GOAL_DarknessBigBrother_WarpAttack1, 10)
    end
    GetWellSpace_Odds = 100
    return GetWellSpace_Odds
    
end

function DarknessBigBrother_Act02(ai, goal, paramTbl)
    local stopDist = 6.25
    local canRunDist = 6.25
    local forceRunMinDist = 6.25 + 999
    local runProbability = 0
    local guardProbability = 0
    local walkLife = 2
    local runLife = 2
    Approach_Act_Flex(ai, goal, stopDist, canRunDist, forceRunMinDist, runProbability, guardProbability, walkLife, runLife)
    local animationId = 3011
    local animationId_2 = 3012
    local animationId_3 = 3010
    local animationId_4 = 3024
    local animationId_5 = 3026
    local animationId_6 = 3025
    local f4_local13 = 3027
    local f4_local14 = 3029
    local successDistance = 999
    local successDistance_2 = 8.42
    local successDistance_3 = 6.73
    local successDistance_4 = 8.94
    local successDistance_5 = 999
    local turnTime = 0
    local turnFaceAngle = 0
    local distanceEnemy = ai:GetDist(TARGET_ENE_0)
    local random = ai:GetRandam_Int(1, 100)
    if random <= 40 then
        goal:AddSubGoal(GOAL_COMMON_ComboAttackTunableSpin, 10, animationId, TARGET_ENE_0, successDistance_2, turnTime, turnFaceAngle, 0, 0)
        goal:AddSubGoal(GOAL_COMMON_ComboFinal, 10, animationId_2, TARGET_ENE_0, successDistance_5, 0, 0)
    elseif random <= 70 then
        goal:AddSubGoal(GOAL_COMMON_ComboAttackTunableSpin, 10, animationId, TARGET_ENE_0, successDistance_2, turnTime, turnFaceAngle, 0, 0)
        goal:AddSubGoal(GOAL_COMMON_ComboFinal, 10, animationId_3, TARGET_ENE_0, successDistance_5, 0, 0)
    elseif random <= 80 then
        ai:SetNumber(1, 1)
        goal:AddSubGoal(GOAL_COMMON_ComboAttackTunableSpin, 10, animationId, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle, 0, 0)
        goal:AddSubGoal(GOAL_COMMON_ComboRepeat_SuccessAngle180, 10, animationId_2, TARGET_ENE_0, 999, 0, 0)
        goal:AddSubGoal(GOAL_COMMON_ComboRepeat_SuccessAngle180, 10, animationId_6, TARGET_ENE_0, 999, 0, 0)
        goal:AddSubGoal(GOAL_DarknessBigBrother_WarpAttack1, 10)
    elseif random <= 90 then
        ai:SetNumber(1, 2)
        goal:AddSubGoal(GOAL_COMMON_ComboAttackTunableSpin, 10, animationId, TARGET_ENE_0, successDistance_3, turnTime, turnFaceAngle, 0, 0)
        goal:AddSubGoal(GOAL_COMMON_ComboRepeat_SuccessAngle180, 10, animationId_4, TARGET_ENE_0, 999, 0, 0)
        goal:AddSubGoal(GOAL_DarknessBigBrother_WarpAttack1, 10)
    else
        ai:SetNumber(1, 3)
        goal:AddSubGoal(GOAL_COMMON_ComboAttackTunableSpin, 10, animationId, TARGET_ENE_0, successDistance_4, turnTime, turnFaceAngle, 0, 0)
        goal:AddSubGoal(GOAL_COMMON_ComboRepeat_SuccessAngle180, 10, animationId_5, TARGET_ENE_0, 999, 0, 0)
        goal:AddSubGoal(GOAL_DarknessBigBrother_WarpAttack1, 10)
    end
    GetWellSpace_Odds = 100
    return GetWellSpace_Odds
    
end

function DarknessBigBrother_Act03(ai, goal, paramTbl)
    if ai:GetNpcThinkParamID() == 525000 then
        local stopDist = 8.47
        local canRunDist = 8.47
        local forceRunMinDist = 8.47 + 999
        local runProbability = 0
        local guardProbability = 0
        local walkLife = 2
        local runLife = 2
        Approach_Act_Flex(ai, goal, stopDist, canRunDist, forceRunMinDist, runProbability, guardProbability, walkLife, runLife)
        local animationId = 3005
        local successDistance = 999
        local turnTime = 0
        local turnFaceAngle = 0
        goal:AddSubGoal(GOAL_COMMON_ComboTunable_SuccessAngle180, 10, animationId, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle, 0, 0)
        GetWellSpace_Odds = 100
        return GetWellSpace_Odds
    else
        local stopDist = 8.47
        local canRunDist = 8.47
        local forceRunMinDist = 8.47 + 999
        local runProbability = 0
        local guardProbability = 0
        local walkLife = 2
        local runLife = 2
        Approach_Act_Flex(ai, goal, stopDist, canRunDist, forceRunMinDist, runProbability, guardProbability, walkLife, runLife)
        local animationId = 3032
        local successDistance = 999
        local turnTime = 0
        local turnFaceAngle = 0
        goal:AddSubGoal(GOAL_COMMON_ComboTunable_SuccessAngle180, 10, animationId, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle, 0, 0)
        GetWellSpace_Odds = 100
        return GetWellSpace_Odds
    end
    
end

function DarknessBigBrother_Act04(ai, goal, paramTbl)
    local stopDist = 5.28
    local canRunDist = 5.28
    local forceRunMinDist = 5.28 + 999
    local runProbability = 0
    local guardProbability = 0
    local walkLife = 2
    local runLife = 2
    Approach_Act_Flex(ai, goal, stopDist, canRunDist, forceRunMinDist, runProbability, guardProbability, walkLife, runLife)
    local animationId = 3006
    local successDistance = 999
    local turnTime = 0
    local turnFaceAngle = 0
    goal:AddSubGoal(GOAL_COMMON_ComboAttackTunableSpin, 10, animationId, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle, 0, 0):SetTargetAngle(1, 0, 90)
    GetWellSpace_Odds = 100
    return GetWellSpace_Odds
    
end

function DarknessBigBrother_Act05(ai, goal, paramTbl)
    local stopDist = 7.96
    local canRunDist = 7.96
    local forceRunMinDist = 7.96 + 999
    local runProbability = 0
    local guardProbability = 0
    local walkLife = 2
    local runLife = 2
    Approach_Act_Flex(ai, goal, stopDist, canRunDist, forceRunMinDist, runProbability, guardProbability, walkLife, runLife)
    local animationId = 3007
    local successDistance = 999
    local turnTime = 0
    local turnFaceAngle = 0
    goal:AddSubGoal(GOAL_COMMON_AttackTunableSpin, 10, animationId, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle, 0, 0)
    GetWellSpace_Odds = 100
    return GetWellSpace_Odds
    
end

function DarknessBigBrother_Act06(ai, goal, paramTbl)
    local stopDist = 11.75
    local canRunDist = 11.75
    local forceRunMinDist = 11.75 + 999
    local runProbability = 0
    local guardProbability = 0
    local walkLife = 2
    local runLife = 2
    Approach_Act_Flex(ai, goal, stopDist, canRunDist, forceRunMinDist, runProbability, guardProbability, walkLife, runLife)
    local animationId = 3013
    local successDistance = 999
    local turnTime = 0
    local turnFaceAngle = 0
    goal:AddSubGoal(GOAL_COMMON_AttackTunableSpin, 10, animationId, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle, 0, 0)
    GetWellSpace_Odds = 100
    return GetWellSpace_Odds
    
end

function DarknessBigBrother_Act07(ai, goal, paramTbl)
    local stopDist = 10.91
    local canRunDist = 10.91
    local forceRunMinDist = 10.91 + 999
    local runProbability = 0
    local guardProbability = 0
    local walkLife = 2
    local runLife = 2
    Approach_Act_Flex(ai, goal, stopDist, canRunDist, forceRunMinDist, runProbability, guardProbability, walkLife, runLife)
    local animationId = 3035
    local successDistance = 999
    local turnTime = 0
    local turnFaceAngle = 0
    goal:AddSubGoal(GOAL_COMMON_AttackTunableSpin, 10, animationId, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle, 0, 0)
    GetWellSpace_Odds = 100
    return GetWellSpace_Odds
    
end

function DarknessBigBrother_Act08(ai, goal, paramTbl)
    ai:SetNumber(1, 4)
    local animationId = 3002
    local turnTime = 0
    local turnFaceAngle = 0
    goal:AddSubGoal(GOAL_COMMON_ComboTunable_SuccessAngle180, 10, animationId, TARGET_ENE_0, 999, turnTime, turnFaceAngle, 0, 0)
    goal:AddSubGoal(GOAL_DarknessBigBrother_WarpAttack1, 10)
    GetWellSpace_Odds = 100
    return GetWellSpace_Odds
    
end

function DarknessBigBrother_Act09(ai, goal, paramTbl)
    ai:SetNumber(1, 5)
    local animationId = 3002
    local turnTime = 0
    local turnFaceAngle = 0
    goal:AddSubGoal(GOAL_COMMON_ComboTunable_SuccessAngle180, 10, animationId, TARGET_ENE_0, 999, turnTime, turnFaceAngle, 0, 0)
    goal:AddSubGoal(GOAL_DarknessBigBrother_WarpAttack1, 10)
    GetWellSpace_Odds = 100
    return GetWellSpace_Odds
    
end

function DarknessBigBrother_Act10(ai, goal, paramTbl)
    if ai:GetNpcThinkParamID() == 525000 then
        ai:SetNumber(1, 6)
        local animationId = 3002
        local turnTime = 0
        local turnFaceAngle = 0
        goal:AddSubGoal(GOAL_COMMON_ComboTunable_SuccessAngle180, 10, animationId, TARGET_ENE_0, 999, turnTime, turnFaceAngle, 0, 180)
        goal:AddSubGoal(GOAL_DarknessBigBrother_WarpAttack1, 10)
        GetWellSpace_Odds = 100
        return GetWellSpace_Odds
    else
        ai:SetNumber(1, 7)
        local animationId = 3002
        local turnTime = 0
        local turnFaceAngle = 0
        goal:AddSubGoal(GOAL_COMMON_ComboTunable_SuccessAngle180, 10, animationId, TARGET_ENE_0, 999, turnTime, turnFaceAngle, 0, 180)
        goal:AddSubGoal(GOAL_DarknessBigBrother_WarpAttack1, 10)
        GetWellSpace_Odds = 100
        return GetWellSpace_Odds
    end
    
end

function DarknessBigBrother_Act11(ai, goal, paramTbl)
    local animationId = 3008
    local successDistance = 999
    local turnTime = 0
    local turnFaceAngle = 0
    goal:AddSubGoal(GOAL_COMMON_ComboTunable_SuccessAngle180, 10, animationId, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle, 0, 0)
    GetWellSpace_Odds = 100
    return GetWellSpace_Odds
    
end

function DarknessBigBrother_Act12(ai, goal, paramTbl)
    local animationId = 3009
    local successDistance = 999
    local turnTime = 0
    local turnFaceAngle = 0
    goal:AddSubGoal(GOAL_COMMON_ComboTunable_SuccessAngle180, 10, animationId, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle, 0, 0)
    GetWellSpace_Odds = 100
    return GetWellSpace_Odds
    
end

function DarknessBigBrother_Act13(ai, goal, paramTbl)
    local animationId = 3019
    local successDistance = 999
    local turnTime = 0
    local turnFaceAngle = 0
    goal:AddSubGoal(GOAL_COMMON_ComboTunable_SuccessAngle180, 10, animationId, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle, 0, 0)
    GetWellSpace_Odds = 100
    return GetWellSpace_Odds
    
end

function DarknessBigBrother_Act14(ai, goal, paramTbl)
    ai:SetNumber(1, 8)
    local animationId = 3015
    local turnTime = 0
    local turnFaceAngle = 0
    goal:AddSubGoal(GOAL_COMMON_ComboTunable_SuccessAngle180, 10, animationId, TARGET_ENE_0, 999, turnTime, turnFaceAngle, 0, 180)
    goal:AddSubGoal(GOAL_DarknessBigBrother_WarpAttack1, 10)
    GetWellSpace_Odds = 100
    return GetWellSpace_Odds
    
end

function DarknessBigBrother_Act15(ai, goal, paramTbl)
    local animationId = 3023
    local successDistance = 999
    local turnTime = 0
    local turnFaceAngle = 0
    goal:AddSubGoal(GOAL_COMMON_AttackTunableSpin, 10, animationId, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle, 0, 0)
    GetWellSpace_Odds = 100
    return GetWellSpace_Odds
    
end

function DarknessBigBrother_Act16(ai, goal, paramTbl)
    local animationId = 3034
    local successDistance = 999
    local turnTime = 0
    local turnFaceAngle = 0
    goal:AddSubGoal(GOAL_COMMON_AttackTunableSpin, 10, animationId, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle, 0, 0)
    GetWellSpace_Odds = 100
    return GetWellSpace_Odds
    
end

function DarknessBigBrother_Act20(ai, goal, paramTbl)
    goal:AddSubGoal(GOAL_COMMON_Turn, 3, TARGET_ENE_0, 90)
    return 0
    
end

function DarknessBigBrother_Act21(ai, goal, paramTbl)
    if ai:IsInsideTarget(TARGET_ENE_0, AI_DIR_TYPE_R, 180) then
        goal:AddSubGoal(GOAL_COMMON_SidewayMove, 1.5, TARGET_ENE_0, 0, ai:GetRandam_Int(30, 45), true, true, 0)
    else
        goal:AddSubGoal(GOAL_COMMON_SidewayMove, 1.5, TARGET_ENE_0, 1, ai:GetRandam_Int(30, 45), true, true, 0)
    end
    GetWellSpace_Odds = 0
    return GetWellSpace_Odds
    
end

function DarknessBigBrother_Act22(ai, goal, paramTbl)
    goal:AddSubGoal(GOAL_COMMON_ApproachTarget, 5, TARGET_ENE_0, 6, TARGET_SELF, true, -1)
    GetWellSpace_Odds = 0
    return GetWellSpace_Odds
    
end

function DarknessBigBrother_Act23(ai, goal, paramTbl)
    goal:AddSubGoal(GOAL_COMMON_Wait, 0.5, TARGET_NONE, 0, 0, 0)
    GetWellSpace_Odds = 0
    return GetWellSpace_Odds
    
end

function DarknessBigBrother_Act24(ai, goal, paramTbl)
    ai:SetNumber(1, 99)
    goal:AddSubGoal(GOAL_COMMON_ComboTunable_SuccessAngle180, 10, 3015, TARGET_ENE_0, 999, 0, 0, 0, 0)
    goal:AddSubGoal(GOAL_DarknessBigBrother_WarpAttack1, 10)
    GetWellSpace_Odds = 0
    return GetWellSpace_Odds
    
end

Goal.Update = function (self, ai, goal)
    return Update_Default_NoSubGoal(self, ai, goal)
    
end

Goal.Terminate = function (self, ai, goal)
    
end

Goal.Interrupt = function (self, ai, goal)
    local random = ai:GetRandam_Int(1, 100)
    local distanceEnemy = ai:GetDist(TARGET_ENE_0)
    if ai:IsLadderAct(TARGET_SELF) then
        return false
    end
    ai:AddObserveSpecialEffectAttribute(TARGET_SELF, 5025)
    if ai:GetSpecialEffectActivateInterruptType(0) == 5025 and distanceEnemy <= 4 and random <= 80 then
        if ai:IsInsideTarget(TARGET_ENE_0, AI_DIR_TYPE_L, 180) then
            local animationId = 3021
            local f26_local3 = 0
            goal:ClearSubGoal()
            goal:AddSubGoal(GOAL_COMMON_ComboFinal, 10, animationId, TARGET_ENE_0, 999, 0)
        else
            local animationId = 3020
            local f26_local3 = 0
            goal:ClearSubGoal()
            goal:AddSubGoal(GOAL_COMMON_ComboFinal, 10, animationId, TARGET_ENE_0, 999, 0)
        end
        return true
    end
    ai:AddObserveSpecialEffectAttribute(TARGET_SELF, 5026)
    if ai:GetSpecialEffectActivateInterruptType(0) == 5026 and ai:IsInsideMsbRegion(TARGET_ENE_0, AI_DIR_TYPE_F, 0, 3412890) == true then
        local animationId = 3025
        local f26_local3 = 0
        ai:SetNumber(1, 1)
        goal:ClearSubGoal()
        goal:AddSubGoal(GOAL_COMMON_ComboRepeat_SuccessAngle180, 10, animationId, TARGET_ENE_0, 999, 0, 0)
        goal:AddSubGoal(GOAL_DarknessBigBrother_WarpAttack1, 10)
        return true
    end
    ai:AddObserveSpecialEffectAttribute(TARGET_SELF, 5027)
    if ai:GetSpecialEffectActivateInterruptType(0) == 5027 then
        local animationId = 3020
        local animationId_2 = 3024
        local f26_local4 = 0
        ai:SetNumber(1, 2)
        goal:ClearSubGoal()
        goal:AddSubGoal(GOAL_COMMON_ComboRepeat_SuccessAngle180, 10, animationId, TARGET_ENE_0, 999, 0, 0)
        goal:AddSubGoal(GOAL_COMMON_ComboRepeat_SuccessAngle180, 10, animationId_2, TARGET_ENE_0, 999, 0, 0)
        goal:AddSubGoal(GOAL_DarknessBigBrother_WarpAttack1, 10)
        return true
    end
    ai:AddObserveSpecialEffectAttribute(TARGET_SELF, 5028)
    if ai:GetSpecialEffectActivateInterruptType(0) == 5028 then
        if random <= 30 then
            goal:ClearSubGoal()
            DarknessBigBrother_Act08(ai, goal, paramTbl)
        else
            goal:ClearSubGoal()
            DarknessBigBrother_Act09(ai, goal, paramTbl)
        end
        return true
    end
    return false
    
end

RegisterTableGoal(GOAL_DarknessBigBrother_WarpAttack1, "DarknessBigBrother_WarpAttack1")
REGISTER_GOAL_NO_SUB_GOAL(GOAL_DarknessBigBrother_WarpAttack1, true)

Goal.Activate = function (self, ai, goal)
    local f27_local0 = 3016
    local f27_local1 = 0
    local f27_local2 = 1
    local f27_local3 = 0
    local f27_local4 = 0
    local f27_local5 = 0
    local f27_local6 = 0
    local f27_local7 = 0
    local f27_local8 = 0
    local f27_local9 = 0
    local f27_local10 = 0
    local f27_local11 = 0
    local f27_local12 = 0
    local f27_local13 = 0
    local f27_local14 = 0
    local f27_local15 = 0
    local f27_local16 = 0
    local f27_local17 = 0
    local f27_local18 = 0
    if ai:GetNumber(1) == 1 then
        f27_local0 = 3028
        f27_local1 = 3018
        f27_local2 = 50
        f27_local3 = 0
        f27_local4 = 0
        f27_local5 = 0
        f27_local6 = 50
        f27_local7 = 0
        f27_local8 = 0
        f27_local9 = 0
        f27_local10 = 9.5
        f27_local11 = 0
        f27_local12 = 0
        f27_local13 = 0
        f27_local14 = 8.1
        f27_local15 = 0
        f27_local16 = 0
        f27_local17 = 0
        f27_local18 = 0
    elseif ai:GetNumber(1) == 2 then
        f27_local0 = 3027
        f27_local1 = 0
        f27_local2 = 1
        f27_local3 = 33
        f27_local4 = 33
        f27_local5 = 33
        f27_local6 = 0
        f27_local7 = 0
        f27_local8 = 0
        f27_local9 = 0
        f27_local10 = 6.6
        f27_local11 = 3.6
        f27_local12 = 3.6
        f27_local13 = 3.6
        f27_local14 = 0
        f27_local15 = 0
        f27_local16 = 0
        f27_local17 = 0
        f27_local18 = 0
    elseif ai:GetNumber(1) == 3 then
        f27_local0 = 3029
        f27_local1 = 0
        f27_local2 = 1
        f27_local3 = 33
        f27_local4 = 33
        f27_local5 = 33
        f27_local6 = 0
        f27_local7 = 0
        f27_local8 = 0
        f27_local9 = 0
        f27_local10 = 6.6
        f27_local11 = 3.6
        f27_local12 = 3.6
        f27_local13 = 3.6
        f27_local14 = 0
        f27_local15 = 0
        f27_local16 = 0
        f27_local17 = 0
        f27_local18 = 1
    elseif ai:GetNumber(1) == 4 then
        f27_local0 = 3018
        f27_local1 = 3033
        f27_local2 = 10
        f27_local3 = 10
        f27_local4 = 10
        f27_local5 = 10
        f27_local6 = 10
        f27_local7 = 10
        f27_local8 = 10
        f27_local9 = 10
        f27_local10 = 3.6
        f27_local11 = 3.6
        f27_local12 = 3.6
        f27_local13 = 3.6
        f27_local14 = 3.6
        f27_local15 = 3.6
        f27_local16 = 3.6
        f27_local17 = 3.6
        f27_local18 = 1
    elseif ai:GetNumber(1) == 5 then
        f27_local0 = 3003
        f27_local1 = 3004
        f27_local2 = 10
        f27_local3 = 10
        f27_local4 = 15
        f27_local5 = 15
        f27_local6 = 10
        f27_local7 = 10
        f27_local8 = 15
        f27_local9 = 15
        f27_local10 = 3.6
        f27_local11 = 3.6
        f27_local12 = 3.6
        f27_local13 = 3.6
        f27_local14 = 3.6
        f27_local15 = 3.6
        f27_local16 = 3.6
        f27_local17 = 3.6
        f27_local18 = 1
    elseif ai:GetNumber(1) == 6 then
        f27_local0 = 3022
        f27_local1 = 0
        f27_local2 = 1
        f27_local3 = 33
        f27_local4 = 33
        f27_local5 = 33
        f27_local6 = 0
        f27_local7 = 0
        f27_local8 = 0
        f27_local9 = 0
        f27_local10 = 15
        f27_local11 = 15
        f27_local12 = 15
        f27_local13 = 15
        f27_local14 = 0
        f27_local15 = 0
        f27_local16 = 0
        f27_local17 = 0
        f27_local18 = 0
    elseif ai:GetNumber(1) == 7 then
        f27_local0 = 3036
        f27_local1 = 0
        f27_local2 = 85
        f27_local3 = 5
        f27_local4 = 5
        f27_local5 = 5
        f27_local6 = 0
        f27_local7 = 0
        f27_local8 = 0
        f27_local9 = 0
        f27_local10 = 15
        f27_local11 = 15
        f27_local12 = 15
        f27_local13 = 15
        f27_local14 = 0
        f27_local15 = 0
        f27_local16 = 0
        f27_local17 = 0
        f27_local18 = 0
    elseif ai:GetNumber(1) == 8 then
        f27_local0 = 3017
        f27_local1 = 0
        f27_local2 = 97
        f27_local3 = 1
        f27_local4 = 1
        f27_local5 = 1
        f27_local6 = 0
        f27_local7 = 0
        f27_local8 = 0
        f27_local9 = 0
        f27_local10 = 15
        f27_local11 = 15
        f27_local12 = 15
        f27_local13 = 15
        f27_local14 = 0
        f27_local15 = 0
        f27_local16 = 0
        f27_local17 = 0
        f27_local18 = 0
    else
        f27_local0 = 3016
        f27_local1 = 0
        f27_local2 = 25
        f27_local3 = 25
        f27_local4 = 25
        f27_local5 = 25
        f27_local6 = 0
        f27_local7 = 0
        f27_local8 = 0
        f27_local9 = 0
        f27_local10 = 0
        f27_local11 = 0
        f27_local12 = 0
        f27_local13 = 0
        f27_local14 = 0
        f27_local15 = 0
        f27_local16 = 0
        f27_local17 = 0
        f27_local18 = 0
    end
    local successDistance = 999
    local f27_local20 = 0
    local f27_local21 = 0
    local lineWidth = ai:GetMapHitRadius(TARGET_SELF)
    local distanceEnemy = ai:GetDist(TARGET_ENE_0)
    local angleToEnemy = ai:GetRelativeAngleFromTarget(TARGET_ENE_0)
    local f27_local25 = 3016
    if f27_local18 ~= 0 then
        if math.abs(angleToEnemy) <= 45 then
            f27_local2 = 0
            f27_local6 = 0
        elseif angleToEnemy > 45 and angleToEnemy < 135 then
            f27_local5 = 0
            f27_local9 = 0
        elseif angleToEnemy < -45 and angleToEnemy > -135 then
            f27_local4 = 0
            f27_local8 = 0
        else
            f27_local3 = 0
            f27_local7 = 0
        end
    end
    if ai:GetExistMeshOnLineDistEx(TARGET_ENE_0, AI_DIR_TYPE_F, f27_local10 + lineWidth, lineWidth, 0) <= f27_local10 then
        f27_local2 = 0
    end
    if ai:GetExistMeshOnLineDistEx(TARGET_ENE_0, AI_DIR_TYPE_B, f27_local11 + lineWidth, lineWidth, 0) <= f27_local11 then
        f27_local3 = 0
    end
    if ai:GetExistMeshOnLineDistEx(TARGET_ENE_0, AI_DIR_TYPE_L, f27_local12 + lineWidth, lineWidth, 0) <= f27_local12 then
        f27_local4 = 0
    end
    if ai:GetExistMeshOnLineDistEx(TARGET_ENE_0, AI_DIR_TYPE_R, f27_local13 + lineWidth, lineWidth, 0) <= f27_local13 then
        f27_local5 = 0
    end
    if ai:GetExistMeshOnLineDistEx(TARGET_ENE_0, AI_DIR_TYPE_F, f27_local14 + lineWidth, lineWidth, 0) <= f27_local14 then
        f27_local6 = 0
    end
    if ai:GetExistMeshOnLineDistEx(TARGET_ENE_0, AI_DIR_TYPE_B, f27_local15 + lineWidth, lineWidth, 0) <= f27_local15 then
        f27_local7 = 0
    end
    if ai:GetExistMeshOnLineDistEx(TARGET_ENE_0, AI_DIR_TYPE_L, f27_local16 + lineWidth, lineWidth, 0) <= f27_local16 then
        f27_local8 = 0
    end
    if ai:GetExistMeshOnLineDistEx(TARGET_ENE_0, AI_DIR_TYPE_R, f27_local17 + lineWidth, lineWidth, 0) <= f27_local17 then
        f27_local7 = 0
    end
    local f27_local26 = nil
    if ai:IsInsideMsbRegion(TARGET_ENE_0, AI_DIR_TYPE_F, f27_local10, 3412835) == true then
        f27_local2 = 0
    end
    if ai:IsInsideMsbRegion(TARGET_ENE_0, AI_DIR_TYPE_B, f27_local11, 3412835) == true then
        f27_local3 = 0
    end
    if ai:IsInsideMsbRegion(TARGET_ENE_0, AI_DIR_TYPE_L, f27_local12, 3412835) == true then
        f27_local4 = 0
    end
    if ai:IsInsideMsbRegion(TARGET_ENE_0, AI_DIR_TYPE_R, f27_local13, 3412835) == true then
        f27_local5 = 0
    end
    if ai:IsInsideMsbRegion(TARGET_ENE_0, AI_DIR_TYPE_F, f27_local14, 3412835) == true then
        f27_local6 = 0
    end
    if ai:IsInsideMsbRegion(TARGET_ENE_0, AI_DIR_TYPE_B, f27_local15, 3412835) == true then
        f27_local7 = 0
    end
    if ai:IsInsideMsbRegion(TARGET_ENE_0, AI_DIR_TYPE_L, f27_local16, 3412835) == true then
        f27_local8 = 0
    end
    if ai:IsInsideMsbRegion(TARGET_ENE_0, AI_DIR_TYPE_R, f27_local17, 3412835) == true then
        f27_local7 = 0
    end
    for f27_local27 = 0, 18, 1 do
        if ai:IsInsideMsbRegion(TARGET_ENE_0, AI_DIR_TYPE_F, f27_local10, 3412870 + f27_local27) == true then
            f27_local2 = 0
        end
        if ai:IsInsideMsbRegion(TARGET_ENE_0, AI_DIR_TYPE_B, f27_local11, 3412870 + f27_local27) == true then
            f27_local3 = 0
        end
        if ai:IsInsideMsbRegion(TARGET_ENE_0, AI_DIR_TYPE_L, f27_local12, 3412870 + f27_local27) == true then
            f27_local4 = 0
        end
        if ai:IsInsideMsbRegion(TARGET_ENE_0, AI_DIR_TYPE_R, f27_local13, 3412870 + f27_local27) == true then
            f27_local5 = 0
        end
        if ai:IsInsideMsbRegion(TARGET_ENE_0, AI_DIR_TYPE_F, f27_local14, 3412870 + f27_local27) == true then
            f27_local6 = 0
        end
        if ai:IsInsideMsbRegion(TARGET_ENE_0, AI_DIR_TYPE_B, f27_local15, 3412870 + f27_local27) == true then
            f27_local7 = 0
        end
        if ai:IsInsideMsbRegion(TARGET_ENE_0, AI_DIR_TYPE_L, f27_local16, 3412870 + f27_local27) == true then
            f27_local8 = 0
        end
        if ai:IsInsideMsbRegion(TARGET_ENE_0, AI_DIR_TYPE_R, f27_local17, 3412870 + f27_local27) == true then
            f27_local9 = 0
        end
    end
    local random = ai:GetRandam_Int(0, f27_local2 + f27_local3 + f27_local4 + f27_local5 + f27_local6 + f27_local7 + f27_local8 + f27_local9)
    local orientationFromTarget = AI_DIR_TYPE_F
    local distanceFromTarget = 0
    local animationId = f27_local25
    local turnTarget = TARGET_ENE_0
    if f27_local2 + f27_local3 + f27_local4 + f27_local5 + f27_local6 + f27_local7 + f27_local8 + f27_local9 == 0 then
        orientationFromTarget = AI_DIR_TYPE_F
        distanceFromTarget = 0
        animationId = 3016
    elseif f27_local2 ~= 0 and random <= f27_local2 then
        orientationFromTarget = AI_DIR_TYPE_F
        distanceFromTarget = f27_local10
        animationId = f27_local0
        turnTarget = TARGET_ENE_0
    elseif f27_local3 ~= 0 and random <= f27_local2 + f27_local3 then
        orientationFromTarget = AI_DIR_TYPE_B
        distanceFromTarget = f27_local11
        animationId = f27_local0
        turnTarget = TARGET_ENE_0
    elseif f27_local4 ~= 0 and random <= f27_local2 + f27_local3 + f27_local4 then
        orientationFromTarget = AI_DIR_TYPE_L
        distanceFromTarget = f27_local12
        animationId = f27_local0
        turnTarget = TARGET_ENE_0
    elseif f27_local5 ~= 0 and random <= f27_local2 + f27_local3 + f27_local4 + f27_local5 then
        orientationFromTarget = AI_DIR_TYPE_R
        distanceFromTarget = f27_local13
        animationId = f27_local0
        turnTarget = TARGET_ENE_0
    elseif f27_local6 ~= 0 and random <= f27_local2 + f27_local3 + f27_local4 + f27_local5 + f27_local6 then
        orientationFromTarget = AI_DIR_TYPE_F
        distanceFromTarget = f27_local14
        animationId = f27_local1
        turnTarget = TARGET_ENE_0
    elseif f27_local7 ~= 0 and random <= f27_local2 + f27_local3 + f27_local4 + f27_local5 + f27_local6 + f27_local7 then
        orientationFromTarget = AI_DIR_TYPE_B
        distanceFromTarget = f27_local15
        animationId = f27_local1
        turnTarget = TARGET_ENE_0
    elseif f27_local8 ~= 0 and random <= f27_local2 + f27_local3 + f27_local4 + f27_local5 + f27_local6 + f27_local7 + f27_local8 then
        orientationFromTarget = AI_DIR_TYPE_L
        distanceFromTarget = f27_local16
        animationId = f27_local1
        turnTarget = TARGET_ENE_0
    else
        orientationFromTarget = AI_DIR_TYPE_R
        distanceFromTarget = f27_local17
        animationId = f27_local1
        turnTarget = TARGET_ENE_0
    end
    ai:SetNumber(2, 1)
    if ai:IsInsideMsbRegion(TARGET_ENE_0, AI_DIR_TYPE_F, 0, 3412890) == false then
        goal:AddSubGoal(GOAL_COMMON_ComboRepeat_SuccessAngle180, 10, 3004, TARGET_ENE_0, successDistance, 0, 0)
    elseif ai:IsInsideMsbRegion(TARGET_SELF, AI_DIR_TYPE_F, 0, 3412860) == true then
        ai:SetEventMoveTarget(3412850)
        goal:AddSubGoal(GOAL_COMMON_ToTargetWarp, 10, POINT_EVENT, AI_DIR_TYPE_F, 0, TARGET_ENE_0, 5, -2)
        goal:AddSubGoal(GOAL_COMMON_ComboRepeat_SuccessAngle180, 10, 3016, TARGET_ENE_0, successDistance, 0, 0)
    elseif ai:IsInsideMsbRegion(TARGET_ENE_0, AI_DIR_TYPE_F, 0, 3412835) or ai:IsInsideMsbRegion(TARGET_ENE_0, AI_DIR_TYPE_F, 0, 3412860) then
        goal:AddSubGoal(GOAL_COMMON_ComboRepeat_SuccessAngle180, 10, 3004, TARGET_ENE_0, successDistance, 0, 0)
    else
        goal:AddSubGoal(GOAL_COMMON_ToTargetWarp, 10, TARGET_ENE_0, orientationFromTarget, distanceFromTarget, turnTarget, 5, -2)
        goal:AddSubGoal(GOAL_COMMON_ComboRepeat_SuccessAngle180, 10, animationId, TARGET_ENE_0, successDistance, 0, 0)
    end
    

end

Goal.Update = function (self, ai, goal)
    return Update_Default_NoSubGoal(self, ai, goal)
    
end

function DarknessBigBrother_ActAfter_AdjustSpace(ai, goal, paramTbl)
    
end


