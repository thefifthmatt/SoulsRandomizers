RegisterTableGoal(GOAL_YUPA620000_Battle, "GOAL_YUPA620000")
REGISTER_GOAL_NO_SUB_GOAL(GOAL_YUPA620000_Battle, true)

Goal.Initialize = function (self, ai, goal, battleActivatedCount)
    ai:SetNumber(0, 0)
    ai:SetNumber(1, 0)
    
end

Goal.Activate = function (self, ai, goal)
    Init_Pseudo_Global(ai, goal)
    ai:SetStringIndexedNumber("AddDistWalk", 0)
    ai:SetStringIndexedNumber("AddDistRun", 0.2)
    ai:SetStringIndexedNumber("Dist_SideStep", 5)
    ai:SetStringIndexedNumber("Dist_BackStep", 5)
    local probabilities = {}
    local acts = {}
    local paramTbls = {}
    Common_Clear_Param(probabilities, acts, paramTbls)
    local distanceEnemy = ai:GetDist(TARGET_ENE_0)
    local random = ai:GetRandam_Int(1, 100)
    local paramAI_EXCEL_THINK_PARAM_TYPE__thinkattr_doAdmirer = ai:GetExcelParam(AI_EXCEL_THINK_PARAM_TYPE__thinkattr_doAdmirer)
    local eventRequest = ai:GetEventRequest()
    local hpRatioSelf = ai:GetHpRate(TARGET_SELF)
    local distanceYEnemy = ai:GetDistYSigned(TARGET_ENE_0)
    if ai:GetNumber(0) == 0 then
        probabilities[24] = 100
    elseif distanceEnemy - distanceYEnemy <= 2 and distanceYEnemy >= 4 then
        probabilities[25] = 100
    elseif ai:HasSpecialEffectId(TARGET_SELF, 16207) == false then
        if hpRatioSelf <= 0.42 then
            probabilities[14] = 100
        elseif ai:IsInsideTarget(TARGET_ENE_0, AI_DIR_TYPE_B, 120) then
            if ai:IsInsideTarget(TARGET_ENE_0, AI_DIR_TYPE_L, 180) then
                probabilities[1] = 1
                probabilities[5] = 80
                probabilities[11] = 10
                probabilities[20] = 10
            else
                probabilities[1] = 1
                probabilities[6] = 80
                probabilities[11] = 10
                probabilities[20] = 10
            end
        elseif distanceEnemy >= 14 then
            probabilities[1] = 1
            probabilities[2] = 0
            probabilities[3] = 36
            probabilities[4] = 18
            probabilities[9] = 0
            probabilities[11] = 0
            probabilities[12] = 0
            probabilities[13] = 0
            probabilities[16] = 36
            probabilities[21] = 0
            probabilities[22] = 0
            probabilities[25] = 10
        elseif distanceEnemy >= 5.3 then
            probabilities[1] = 1
            probabilities[2] = 10
            probabilities[3] = 30
            probabilities[4] = 10
            probabilities[9] = 0
            probabilities[11] = 0
            probabilities[12] = 20
            probabilities[13] = 0
            probabilities[16] = 30
            probabilities[21] = 0
            probabilities[22] = 0
            probabilities[25] = 0
            if distanceEnemy <= 7 then
                probabilities[16] = 0
            end
        elseif distanceEnemy >= 2.3 then
            probabilities[1] = 1
            probabilities[2] = 30
            probabilities[3] = 0
            probabilities[4] = 0
            probabilities[9] = 0
            probabilities[11] = 0
            probabilities[12] = 40
            probabilities[13] = 20
            probabilities[21] = 10
            probabilities[22] = 0
        else
            probabilities[1] = 25
            probabilities[2] = 0
            probabilities[3] = 0
            probabilities[4] = 15
            probabilities[9] = 0
            probabilities[11] = 20
            probabilities[12] = 0
            probabilities[13] = 20
            probabilities[21] = 0
            probabilities[22] = 20
            if distanceEnemy >= 1.8 then
                probabilities[11] = 0
            end
            if distanceEnemy <= 0.8 then
                probabilities[22] = 0
            end
        end
    elseif ai:IsInsideTarget(TARGET_ENE_0, AI_DIR_TYPE_B, 90) then
        if ai:IsInsideTarget(TARGET_ENE_0, AI_DIR_TYPE_L, 180) then
            probabilities[1] = 1
            probabilities[5] = 90
            probabilities[11] = 0
            probabilities[20] = 10
        else
            probabilities[1] = 1
            probabilities[6] = 90
            probabilities[11] = 0
            probabilities[20] = 10
        end
    elseif distanceEnemy >= 5.3 then
        probabilities[1] = 1
        probabilities[2] = 0
        probabilities[3] = 36
        probabilities[4] = 36
        probabilities[9] = 0
        probabilities[11] = 0
        probabilities[12] = 0
        probabilities[13] = 0
        probabilities[15] = 27
        probabilities[21] = 0
        probabilities[22] = 0
        probabilities[25] = 10
        if distanceEnemy <= 10 then
            probabilities[25] = 0
        end
    elseif distanceEnemy >= 2.3 then
        probabilities[1] = 5
        probabilities[2] = 25
        probabilities[3] = 0
        probabilities[4] = 20
        probabilities[9] = 0
        probabilities[11] = 0
        probabilities[12] = 0
        probabilities[13] = 20
        probabilities[14] = 20
        probabilities[21] = 0
        probabilities[22] = 0
    else
        probabilities[1] = 5
        probabilities[2] = 0
        probabilities[3] = 0
        probabilities[4] = 30
        probabilities[9] = 0
        probabilities[11] = 0
        probabilities[12] = 0
        probabilities[13] = 45
        probabilities[14] = 20
        probabilities[21] = 0
        probabilities[22] = 0
    end
    probabilities[1] = SetCoolTime(ai, goal, 3000, 9, probabilities[1], 1)
    probabilities[2] = SetCoolTime(ai, goal, 3004, 6, probabilities[2], 0)
    probabilities[3] = SetCoolTime(ai, goal, 3008, 6, probabilities[3], 0)
    probabilities[4] = SetCoolTime(ai, goal, 3010, 25, probabilities[4], 0)
    probabilities[4] = SetCoolTime(ai, goal, 3036, 25, probabilities[4], 0)
    probabilities[5] = SetCoolTime(ai, goal, 3015, 6, probabilities[5], 0)
    probabilities[6] = SetCoolTime(ai, goal, 3016, 6, probabilities[6], 0)
    probabilities[10] = SetCoolTime(ai, goal, 3030, 45, probabilities[10], 1)
    probabilities[10] = SetCoolTime(ai, goal, 3031, 45, probabilities[10], 1)
    probabilities[11] = SetCoolTime(ai, goal, 3022, 15, probabilities[11], 0)
    probabilities[12] = SetCoolTime(ai, goal, 3028, 25, probabilities[12], 0)
    probabilities[13] = SetCoolTime(ai, goal, 3007, 6, probabilities[13], 0)
    probabilities[14] = SetCoolTime(ai, goal, 3031, 60, probabilities[14], 1)
    probabilities[14] = SetCoolTime(ai, goal, 3030, 60, probabilities[14], 1)
    probabilities[15] = SetCoolTime(ai, goal, 3024, 20, probabilities[15], 0)
    probabilities[16] = SetCoolTime(ai, goal, 3026, 20, probabilities[16], 0)
    probabilities[25] = SetCoolTime(ai, goal, 3036, 30, probabilities[25], 1)
    acts[1] = REGIST_FUNC(ai, goal, YUPA620000_Act01)
    acts[2] = REGIST_FUNC(ai, goal, YUPA620000_Act02)
    acts[3] = REGIST_FUNC(ai, goal, YUPA620000_Act03)
    acts[4] = REGIST_FUNC(ai, goal, YUPA620000_Act04)
    acts[5] = REGIST_FUNC(ai, goal, YUPA620000_Act05)
    acts[6] = REGIST_FUNC(ai, goal, YUPA620000_Act06)
    acts[7] = REGIST_FUNC(ai, goal, YUPA620000_Act07)
    acts[8] = REGIST_FUNC(ai, goal, YUPA620000_Act08)
    acts[9] = REGIST_FUNC(ai, goal, YUPA620000_Act09)
    acts[10] = REGIST_FUNC(ai, goal, YUPA620000_Act10)
    acts[11] = REGIST_FUNC(ai, goal, YUPA620000_Act11)
    acts[12] = REGIST_FUNC(ai, goal, YUPA620000_Act12)
    acts[13] = REGIST_FUNC(ai, goal, YUPA620000_Act13)
    acts[14] = REGIST_FUNC(ai, goal, YUPA620000_Act14)
    acts[15] = REGIST_FUNC(ai, goal, YUPA620000_Act15)
    acts[16] = REGIST_FUNC(ai, goal, YUPA620000_Act16)
    acts[20] = REGIST_FUNC(ai, goal, YUPA620000_Act20)
    acts[21] = REGIST_FUNC(ai, goal, YUPA620000_Act21)
    acts[22] = REGIST_FUNC(ai, goal, YUPA620000_Act22)
    acts[23] = REGIST_FUNC(ai, goal, YUPA620000_Act23)
    acts[24] = REGIST_FUNC(ai, goal, YUPA620000_Act24)
    acts[25] = REGIST_FUNC(ai, goal, YUPA620000_Act25)
    local actAfter = REGIST_FUNC(ai, goal, YUPA620000_ActAfter_AdjustSpace)
    Common_Battle_Activate(ai, goal, probabilities, acts, actAfter, paramTbls)
    
end

function YUPA620000_Act01(ai, goal, paramTbl)
    local stopDist = 2.5
    local canRunDist = 2.5 + 8
    local forceRunMinDist = 2.5 + 8
    local runProbability = 0
    local guardProbability = 0
    local walkLife = 1.5
    local runLife = 3
    if ai:HasSpecialEffectId(TARGET_SELF, 16206) == true then
        canRunDist = 0
        forceRunMinDist = 0
        runProbability = 0
    end
    Approach_Act_Flex(ai, goal, stopDist, canRunDist, forceRunMinDist, runProbability, guardProbability, walkLife, runLife)
    local animationId = 3000
    local animationId_2 = 3001
    local f3_local9 = 3002
    local f3_local10 = 999
    local turnTime = 0
    local turnFaceAngle = 0
    local random = ai:GetRandam_Int(1, 100)
    local successDistance = 4.1
    local f3_local15 = 4.2 + 1
    if random <= 30 and ai:HasSpecialEffectId(TARGET_SELF, 16207) == false then
        goal:AddSubGoal(GOAL_COMMON_ComboTunable_SuccessAngle180, 10, animationId, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle, 0, 0)
    else
        goal:AddSubGoal(GOAL_COMMON_ComboTunable_SuccessAngle180, 10, animationId, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle, 0, 0)
        goal:AddSubGoal(GOAL_COMMON_ComboRepeat_SuccessAngle180, 10, animationId_2, TARGET_ENE_0, 999, 0, 0)
    end
    GetWellSpace_Odds = 100
    return GetWellSpace_Odds
    
end

function YUPA620000_Act02(ai, goal, paramTbl)
    local stopDist = 4.2
    local canRunDist = 4.2 + 8
    local forceRunMinDist = 4.2 + 8
    local runProbability = 0
    local guardProbability = 0
    local walkLife = 1.5
    local runLife = 3
    if ai:HasSpecialEffectId(TARGET_SELF, 16206) == true then
        canRunDist = 0
        forceRunMinDist = 0
        runProbability = 0
    end
    Approach_Act_Flex(ai, goal, stopDist, canRunDist, forceRunMinDist, runProbability, guardProbability, walkLife, runLife)
    local f4_local7 = 3004
    local f4_local8 = 3005
    local f4_local9 = 3006
    local f4_local10 = 999
    local turnTime = 0
    local turnFaceAngle = 0
    local random = ai:GetRandam_Int(1, 100)
    local f4_local14 = 4.2 + 2
    local f4_local15 = 3.41 + 2
    goal:AddSubGoal(GOAL_COMMON_ComboAttackTunableSpin, 10, 3004, TARGET_ENE_0, 20, turnTime, turnFaceAngle, 0, 0)
    GetWellSpace_Odds = 100
    return GetWellSpace_Odds
    
end

function YUPA620000_Act03(ai, goal, paramTbl)
    local stopDist = 6.79
    local canRunDist = 6.79 + 8
    local forceRunMinDist = 6.79 + 8
    local runProbability = 0
    local guardProbability = 0
    local walkLife = 1.5
    local runLife = 3
    if ai:HasSpecialEffectId(TARGET_SELF, 16206) == true then
        canRunDist = 0
        forceRunMinDist = 0
        runProbability = 0
    end
    Approach_Act_Flex(ai, goal, stopDist, canRunDist, forceRunMinDist, runProbability, guardProbability, walkLife, runLife)
    local animationId = 3008
    local f5_local8 = 3009
    local f5_local9 = 999
    local turnTime = 0
    local turnFaceAngle = 0
    local random = ai:GetRandam_Int(1, 100)
    local f5_local13 = 8.48 + 2
    goal:AddSubGoal(GOAL_COMMON_ComboTunable_SuccessAngle180, 10, animationId, TARGET_ENE_0, 999, turnTime, turnFaceAngle, 0, 0)
    GetWellSpace_Odds = 100
    return GetWellSpace_Odds
    
end

function YUPA620000_Act04(ai, goal, paramTbl)
    local animationId = 3010
    local f6_local1 = 999
    local turnTime = 0
    local turnFaceAngle = 0
    local random = ai:GetRandam_Int(1, 100)
    local successDistance = 999
    goal:AddSubGoal(GOAL_COMMON_ComboTunable_SuccessAngle180, 10, animationId, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle, 0, 0)
    GetWellSpace_Odds = 100
    return GetWellSpace_Odds
    
end

function YUPA620000_Act05(ai, goal, paramTbl)
    local animationId = 3015
    local animationId_2 = 3001
    local successDistance = 999
    local turnTime = 0
    local turnFaceAngle = 0
    local random = ai:GetRandam_Int(1, 100)
    local successDistance_2 = 4.1
    local f7_local7 = 4.2 + 2
    if random <= 30 then
        goal:AddSubGoal(GOAL_COMMON_AttackTunableSpin, 10, animationId, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle, 0, 0)
    else
        goal:AddSubGoal(GOAL_COMMON_ComboAttackTunableSpin, 10, animationId, TARGET_ENE_0, successDistance_2, turnTime, turnFaceAngle, 0, 0)
        goal:AddSubGoal(GOAL_COMMON_ComboRepeat, 10, animationId_2, TARGET_ENE_0, 12, 0, 0)
    end
    GetWellSpace_Odds = 100
    return GetWellSpace_Odds
    
end

function YUPA620000_Act06(ai, goal, paramTbl)
    local animationId = 3016
    local successDistance = 999
    local turnTime = 0
    local turnFaceAngle = 0
    local random = ai:GetRandam_Int(1, 100)
    goal:AddSubGoal(GOAL_COMMON_ComboAttackTunableSpin, 10, animationId, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle, 0, 0)
    GetWellSpace_Odds = 100
    return GetWellSpace_Odds
    
end

function YUPA620000_Act07(ai, goal, paramTbl)
    local animationId = 3020
    local successDistance = 999
    local turnTime = 0
    local turnFaceAngle = 0
    local random = ai:GetRandam_Int(1, 100)
    goal:AddSubGoal(GOAL_COMMON_AttackTunableSpin, 10, animationId, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle, 0, 0)
    GetWellSpace_Odds = 100
    return GetWellSpace_Odds
    
end

function YUPA620000_Act08(ai, goal, paramTbl)
    local animationId = 3021
    local f10_local1 = 999
    local turnTime = 0
    local turnFaceAngle = 0
    local random = ai:GetRandam_Int(1, 100)
    goal:AddSubGoal(GOAL_COMMON_ComboAttackTunableSpin, 10, animationId, TARGET_ENE_0, 999, turnTime, turnFaceAngle, 0, 0)
    GetWellSpace_Odds = 100
    return GetWellSpace_Odds
    
end

function YUPA620000_Act09(ai, goal, paramTbl)
    local stopDist = 20
    local canRunDist = 30
    local forceRunMinDist = 30
    local runProbability = 0
    local guardProbability = 0
    local walkLife = 1.5
    local runLife = 3
    Approach_Act_Flex(ai, goal, stopDist, canRunDist, forceRunMinDist, runProbability, guardProbability, walkLife, runLife)
    local animationId = 3029
    local successDistance = 999
    local turnTime = 0
    local turnFaceAngle = 0
    local random = ai:GetRandam_Int(1, 100)
    goal:AddSubGoal(GOAL_COMMON_AttackTunableSpin, 4, animationId, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle, 0, 0)
    GetWellSpace_Odds = 100
    return GetWellSpace_Odds
    
end

function YUPA620000_Act10(ai, goal, paramTbl)
    local f12_local0 = 3030
    local f12_local1 = 999
    local turnTime = 0
    local turnFaceAngle = 0
    local random = ai:GetRandam_Int(1, 100)
    goal:AddSubGoal(GOAL_COMMON_ComboTunable_SuccessAngle180, 2, 3030, TARGET_ENE_0, 999, turnTime, turnFaceAngle, 0, 0):SetLifeEndSuccess(true)
    goal:AddSubGoal(GOAL_COMMON_ComboRepeat_SuccessAngle180, 10, 3024, TARGET_ENE_0, 999, 0, 0)
    goal:AddSubGoal(GOAL_COMMON_ComboRepeat_SuccessAngle180, 10, 3001, TARGET_ENE_0, 999, 0, 0)
    GetWellSpace_Odds = 100
    return GetWellSpace_Odds
    
end

function YUPA620000_Act11(ai, goal, paramTbl)
    local f13_local0 = 3022
    local f13_local1 = 999
    local f13_local2 = 0
    local f13_local3 = 0
    local random = ai:GetRandam_Int(1, 100)
    goal:AddSubGoal(GOAL_COMMON_ComboTunable_SuccessAngle180, 10, 3022, TARGET_ENE_0, 999, 0, 0, 0, 0)
    goal:AddSubGoal(GOAL_COMMON_ComboRepeat_SuccessAngle180, 10, 3008, TARGET_ENE_0, 999, 0, 0)
    GetWellSpace_Odds = 100
    return GetWellSpace_Odds
    
end

function YUPA620000_Act12(ai, goal, paramTbl)
    local stopDist = 30
    local canRunDist = 99
    local forceRunMinDist = 99
    local runProbability = 0
    local guardProbability = 0
    local walkLife = 1.5
    local runLife = 3
    Approach_Act_Flex(ai, goal, stopDist, canRunDist, forceRunMinDist, runProbability, guardProbability, walkLife, runLife)
    local animationId = 3028
    local f14_local8 = 999
    local f14_local9 = 0
    local f14_local10 = 0
    local random = ai:GetRandam_Int(1, 100)
    local distanceEnemy = ai:GetDist(TARGET_ENE_0)
    goal:AddSubGoal(GOAL_COMMON_ComboTunable_SuccessAngle180, 10, animationId, TARGET_ENE_0, 999, 0, 0, 0, 0)
    GetWellSpace_Odds = 100
    return GetWellSpace_Odds
    
end

function YUPA620000_Act13(ai, goal, paramTbl)
    local stopDist = 2.5
    local canRunDist = 2.5 + 8
    local forceRunMinDist = 2.5 + 8
    local runProbability = 0
    local guardProbability = 0
    local walkLife = 1.5
    local runLife = 3
    Approach_Act_Flex(ai, goal, stopDist, canRunDist, forceRunMinDist, runProbability, guardProbability, walkLife, runLife)
    local animationId = 3007
    local f15_local8 = 999
    local turnTime = 0
    local turnFaceAngle = 0
    local random = ai:GetRandam_Int(1, 100)
    local successDistance = 8.48 + 2
    goal:AddSubGoal(GOAL_COMMON_ComboAttackTunableSpin, 10, animationId, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle, 0, 0)
    GetWellSpace_Odds = 100
    return GetWellSpace_Odds
    
end

function YUPA620000_Act14(ai, goal, paramTbl)
    local animationId = 3030
    local f16_local1 = 999
    local f16_local2 = 0
    local f16_local3 = 0
    local random = ai:GetRandam_Int(1, 100)
    goal:AddSubGoal(GOAL_COMMON_ComboTunable_SuccessAngle180, 2, animationId, TARGET_ENE_0, 999, 0, 0, 0, 0):SetLifeEndSuccess(true)
    goal:AddSubGoal(GOAL_COMMON_ComboRepeat_SuccessAngle180, 10, 3024, TARGET_ENE_0, 999, 0, 0)
    GetWellSpace_Odds = 100
    return GetWellSpace_Odds
    
end

function YUPA620000_Act15(ai, goal, paramTbl)
    local stopDist = 20
    local canRunDist = 28
    local forceRunMinDist = 28
    local runProbability = 0
    local guardProbability = 0
    local walkLife = 1.5
    local runLife = 3
    Approach_Act_Flex(ai, goal, stopDist, canRunDist, forceRunMinDist, runProbability, guardProbability, walkLife, runLife)
    local f17_local7 = 3024
    local f17_local8 = 999
    local f17_local9 = 0
    local f17_local10 = 0
    local random = ai:GetRandam_Int(1, 100)
    goal:AddSubGoal(GOAL_COMMON_ComboTunable_SuccessAngle180, 10, 3024, TARGET_ENE_0, 999, 0, 0)
    goal:AddSubGoal(GOAL_COMMON_ComboRepeat_SuccessAngle180, 10, 3001, TARGET_ENE_0, 999, 0, 0)
    GetWellSpace_Odds = 100
    return GetWellSpace_Odds
    
end

function YUPA620000_Act16(ai, goal, paramTbl)
    local f18_local0 = 3025
    local f18_local1 = 999
    local f18_local2 = 0
    local f18_local3 = 0
    local random = ai:GetRandam_Int(1, 100)
    ai:SetNumber(1, 0)
    goal:AddSubGoal(GOAL_COMMON_ComboTunable_SuccessAngle180, 10, 3025, TARGET_ENE_0, 999, 0, 0)
    goal:AddSubGoal(GOAL_COMMON_ComboRepeat_SuccessAngle180, 10, 3026, TARGET_ENE_0, 30, 0, 0)
    GetWellSpace_Odds = 100
    return GetWellSpace_Odds
    
end

function YUPA620000_Act20(ai, goal, paramTbl)
    goal:AddSubGoal(GOAL_COMMON_Turn, 2, TARGET_ENE_0, 90)
    GetWellSpace_Odds = 0
    return GetWellSpace_Odds
    
end

function YUPA620000_Act21(ai, goal, paramTbl)
    local random = ai:GetRandam_Int(1, 100)
    local distanceEnemy = ai:GetDist(TARGET_ENE_0)
    if distanceEnemy >= 10 then
        if random <= 50 then
            goal:AddSubGoal(GOAL_COMMON_SidewayMove, 2, TARGET_ENE_0, 0, 60, true, true, 0)
        else
            goal:AddSubGoal(GOAL_COMMON_SidewayMove, 2, TARGET_ENE_0, 1, 60, true, true, 0)
        end
    elseif random <= 50 then
        goal:AddSubGoal(GOAL_COMMON_SidewayMove, 1.4, TARGET_ENE_0, 0, 60, true, true, 0)
    else
        goal:AddSubGoal(GOAL_COMMON_SidewayMove, 1.4, TARGET_ENE_0, 1, 60, true, true, 0)
    end
    GetWellSpace_Odds = 0
    return GetWellSpace_Odds
    
end

function YUPA620000_Act22(ai, goal, paramTbl)
    goal:AddSubGoal(GOAL_COMMON_LeaveTarget, 1.5, TARGET_ENE_0, 3.5, TARGET_ENE_0, true, 0)
    GetWellSpace_Odds = 0
    return GetWellSpace_Odds
    
end

function YUPA620000_Act23(ai, goal, paramTbl)
    goal:AddSubGoal(GOAL_COMMON_Wait, ai:GetRandam_Float(0.5, 1), TARGET_ENE_0)
    GetWellSpace_Odds = 0
    return GetWellSpace_Odds
    
end

function YUPA620000_Act24(ai, goal, paramTbl)
    ai:SetNumber(0, 1)
    goal:AddSubGoal(GOAL_COMMON_Wait, 1, TARGET_NONE)
    goal:AddSubGoal(GOAL_COMMON_ApproachTarget, 5, TARGET_ENE_0, 3, TARGET_SELF, true, 0)
    GetWellSpace_Odds = 0
    return GetWellSpace_Odds
    
end

function YUPA620000_Act25(ai, goal, paramTbl)
    goal:AddSubGoal(GOAL_COMMON_ComboRepeat_SuccessAngle180, 10, 3035, TARGET_ENE_0, 999, 0, 0)
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
    local random_2 = ai:GetRandam_Int(1, 100)
    local distanceEnemy = ai:GetDist(TARGET_ENE_0)
    ai:AddObserveSpecialEffectAttribute(TARGET_SELF, 5025)
    if ai:GetSpecialEffectActivateInterruptType(0) == 5025 and distanceEnemy >= 1 and distanceEnemy < 5.2 and random <= 70 then
        goal:ClearSubGoal()
        goal:AddSubGoal(GOAL_COMMON_ComboRepeat, 10, 3002, TARGET_ENE_0, 999, 0, 0)
    end
    ai:AddObserveSpecialEffectAttribute(TARGET_SELF, 5026)
    if ai:GetSpecialEffectActivateInterruptType(0) == 5026 and random <= 60 and ai:IsInsideTarget(TARGET_ENE_0, AI_DIR_TYPE_B, 240) then
        goal:ClearSubGoal()
        goal:AddSubGoal(GOAL_COMMON_ComboFinal, 10, 3013, TARGET_ENE_0, 999, 0, 0)
    end
    ai:AddObserveSpecialEffectAttribute(TARGET_SELF, 5027)
    if ai:GetSpecialEffectActivateInterruptType(0) == 5027 then
        if not ai:HasSpecialEffectId(TARGET_SELF, 16207) then
            if random <= 80 then
                if ai:IsInsideTarget(TARGET_ENE_0, AI_DIR_TYPE_B, 240) and distanceEnemy <= 3 and random <= 50 then
                    goal:ClearSubGoal()
                    goal:AddSubGoal(GOAL_COMMON_ComboFinal, 10, 3013, TARGET_ENE_0, 999, 0, 0)
                elseif distanceEnemy <= 3 then
                    goal:ClearSubGoal()
                    goal:AddSubGoal(GOAL_COMMON_ComboFinal, 10, 3005, TARGET_ENE_0, 999, 0, 0)
                end
            end
        elseif ai:IsInsideTarget(TARGET_ENE_0, AI_DIR_TYPE_B, 240) and distanceEnemy <= 3 and random <= 50 and ai:HasSpecialEffectId(TARGET_SELF, 16207) then
            goal:ClearSubGoal()
            goal:AddSubGoal(GOAL_COMMON_ComboRepeat_SuccessAngle180, 10, 3013, TARGET_ENE_0, 999, 0, 0)
            goal:AddSubGoal(GOAL_COMMON_ComboFinal, 10, 3002, TARGET_ENE_0, 999, 0, 0)
        elseif distanceEnemy <= 15 then
            goal:ClearSubGoal()
            goal:AddSubGoal(GOAL_COMMON_ComboFinal, 10, 3009, TARGET_ENE_0, 999, 0, 0)
        end
    end
    ai:AddObserveSpecialEffectAttribute(TARGET_SELF, 5028)
    if ai:GetSpecialEffectActivateInterruptType(0) == 5028 then
        if distanceEnemy <= 3.5 and ai:IsInsideTarget(TARGET_ENE_0, AI_DIR_TYPE_F, 180) and not ai:HasSpecialEffectId(TARGET_SELF, 16207) and random <= 60 then
            goal:ClearSubGoal()
            goal:AddSubGoal(GOAL_COMMON_ComboRepeat, 10, 3005, TARGET_ENE_0, 5, 0, 0)
        elseif distanceEnemy <= 10 and ai:IsInsideTarget(TARGET_ENE_0, AI_DIR_TYPE_F, 180) and ai:HasSpecialEffectId(TARGET_SELF, 16207) and random <= 60 then
            goal:ClearSubGoal()
            goal:AddSubGoal(GOAL_COMMON_ComboRepeat, 10, 3018, TARGET_ENE_0, 999, 0, 0)
            goal:AddSubGoal(GOAL_COMMON_ComboRepeat, 10, 3009, TARGET_ENE_0, 999, 0, 0)
        elseif distanceEnemy <= 10 and ai:IsInsideTarget(TARGET_ENE_0, AI_DIR_TYPE_F, 180) and ai:HasSpecialEffectId(TARGET_SELF, 16207) and random <= 80 then
            goal:ClearSubGoal()
            goal:AddSubGoal(GOAL_COMMON_ComboRepeat, 10, 3009, TARGET_ENE_0, 999, 0, 0)
        elseif distanceEnemy <= 5 and ai:IsInsideTarget(TARGET_ENE_0, AI_DIR_TYPE_B, 180) and ai:HasSpecialEffectId(TARGET_SELF, 16207) and random_2 <= 30 then
            goal:ClearSubGoal()
            goal:AddSubGoal(GOAL_COMMON_ComboFinal, 10, 3013, TARGET_ENE_0, 5, 0, 0)
        elseif distanceEnemy <= 5 and ai:IsInsideTarget(TARGET_ENE_0, AI_DIR_TYPE_B, 180) and ai:HasSpecialEffectId(TARGET_SELF, 16207) and random_2 <= 60 then
            goal:ClearSubGoal()
            goal:AddSubGoal(GOAL_COMMON_ComboRepeat, 10, 3013, TARGET_ENE_0, 5, 0, 0)
            goal:AddSubGoal(GOAL_COMMON_ComboFinal, 10, 3002, TARGET_ENE_0, 999, 0, 0)
        end
    end
    ai:AddObserveSpecialEffectAttribute(TARGET_SELF, 5029)
    if ai:GetSpecialEffectActivateInterruptType(0) == 5029 then
        if ai:HasSpecialEffectId(TARGET_SELF, 16207) == false then
            if distanceEnemy < 5 then
                goal:ClearSubGoal()
                goal:AddSubGoal(GOAL_COMMON_ComboFinal, 10, 3011, TARGET_ENE_0, 999, 0, 0)
            else
                goal:ClearSubGoal()
                goal:AddSubGoal(GOAL_COMMON_ComboFinal, 10, 3012, TARGET_ENE_0, 999, 0, 0)
            end
        elseif distanceEnemy < 5 then
            goal:ClearSubGoal()
            goal:AddSubGoal(GOAL_COMMON_ComboRepeat_SuccessAngle180, 10, 3033, TARGET_ENE_0, 999, 0, 0)
            goal:AddSubGoal(GOAL_COMMON_ComboRepeat_SuccessAngle180, 10, 3014, TARGET_ENE_0, 999, 0, 0)
            goal:AddSubGoal(GOAL_COMMON_ComboRepeat_SuccessAngle180, 10, 3017, TARGET_ENE_0, 12, 0, 0)
            goal:AddSubGoal(GOAL_COMMON_LeaveTarget, 1.5, TARGET_ENE_0, 3.5, TARGET_ENE_0, true, 0)
        else
            goal:ClearSubGoal()
            goal:AddSubGoal(GOAL_COMMON_ComboRepeat_SuccessAngle180, 10, 3032, TARGET_ENE_0, 999, 0, 0)
            goal:AddSubGoal(GOAL_COMMON_ComboRepeat_SuccessAngle180, 10, 3034, TARGET_ENE_0, 999, 0, 0)
            goal:AddSubGoal(GOAL_COMMON_ComboRepeat_SuccessAngle180, 10, 3017, TARGET_ENE_0, 12, 0, 0)
            goal:AddSubGoal(GOAL_COMMON_LeaveTarget, 1.5, TARGET_ENE_0, 3.5, TARGET_ENE_0, true, 0)
        end
    end
    ai:AddObserveSpecialEffectAttribute(TARGET_SELF, 5030)
    if ai:GetSpecialEffectActivateInterruptType(0) == 5030 and distanceEnemy <= 3.5 and ai:IsInsideTarget(TARGET_ENE_0, AI_DIR_TYPE_F, 120) and random <= 80 then
        goal:ClearSubGoal()
        goal:AddSubGoal(GOAL_COMMON_ComboRepeat_SuccessAngle180, 10, 3005, TARGET_ENE_0, 999, 0, 0)
    end
    ai:AddObserveSpecialEffectAttribute(TARGET_SELF, 5031)
    if ai:GetSpecialEffectActivateInterruptType(0) == 5031 and distanceEnemy <= 6 then
        goal:ClearSubGoal()
        goal:AddSubGoal(GOAL_COMMON_ComboRepeat_SuccessAngle180, 10, 3002, TARGET_ENE_0, 999, 0, 0)
    end
    ai:AddObserveSpecialEffectAttribute(TARGET_SELF, 5032)
    if ai:GetSpecialEffectActivateInterruptType(0) == 5032 and distanceEnemy >= 7 then
        goal:ClearSubGoal()
        ai:SetNumber(1, 0)
        goal:AddSubGoal(GOAL_COMMON_ComboRepeat_SuccessAngle180, 10, 3025, TARGET_ENE_0, 999, 0, 0)
        goal:AddSubGoal(GOAL_COMMON_ComboRepeat_SuccessAngle180, 10, 3026, TARGET_ENE_0, 999, 0, 0)
    end
    ai:AddObserveSpecialEffectAttribute(TARGET_SELF, 5033)
    if ai:GetSpecialEffectActivateInterruptType(0) == 5033 and distanceEnemy >= 3.5 and ai:IsInsideTarget(TARGET_ENE_0, AI_DIR_TYPE_F, 180) and ai:HasSpecialEffectId(TARGET_SELF, 16207) == false then
        goal:ClearSubGoal()
        goal:AddSubGoal(GOAL_COMMON_ComboFinal, 10, 3003, TARGET_ENE_0, 999, 0, 0)
    end
    ai:AddObserveSpecialEffectAttribute(TARGET_SELF, 5034)
    if ai:GetSpecialEffectActivateInterruptType(0) == 5034 and distanceEnemy <= 3 and not ai:HasSpecialEffectAttribute(TARGET_ENE_0, SP_EFFECT_TYPE_TARGET_DOWN) and ai:IsInsideTarget(TARGET_ENE_0, AI_DIR_TYPE_F, 30) then
        goal:ClearSubGoal()
        goal:AddSubGoal(GOAL_COMMON_ComboFinal, 10, 3006, TARGET_ENE_0, 12, 0, 0)
    end
    ai:AddObserveSpecialEffectAttribute(TARGET_SELF, 5050)
    if ai:GetSpecialEffectActivateInterruptType(0) == 5050 then
        goal:ClearSubGoal()
        if ai:GetHpRate(TARGET_SELF) > 0 and ai:IsInsideMsbRegion(TARGET_ENE_0, AI_DIR_TYPE_F, 0, 5112820) == false and ai:IsInsideMsbRegion(TARGET_ENE_0, AI_DIR_TYPE_F, 0, 5112822) == false then
            goal:AddSubGoal(GOAL_COMMON_ToTargetWarp, 10, TARGET_ENE_0, AI_DIR_TYPE_F, 0, TARGET_ENE_0, 0, 0)
        end
        goal:AddSubGoal(GOAL_COMMON_ComboRepeat_SuccessAngle180, 10, 3036, TARGET_ENE_0, 999, 0, 0)
    end
    if ai:IsInterupt(INTERUPT_Shoot) and distanceEnemy >= 8 then
        goal:AddSubGoal(GOAL_COMMON_ApproachTarget, 2, TARGET_ENE_0, 3, TARGET_SELF, false, 0)
    end
    
end

function YUPA620000_ActAfter_AdjustSpace(ai, goal, paramTbl)
    goal:AddSubGoal(GOAL_YUPA620000_Battle_AfterAttackAct, 10)
    
end

RegisterTableGoal(GOAL_YUPA620000_Battle_AfterAttackAct, "GOAL_YUPA620000_Battle_AfterAttackAct")
REGISTER_GOAL_NO_SUB_GOAL(GOAL_YUPA620000_Battle_AfterAttackAct, true)

Goal.Activate = function (self, ai, goal)
    local distanceEnemy = ai:GetDist(TARGET_ENE_0)
    local random = ai:GetRandam_Int(1, 100)
    
end

Goal.Update = function (self, ai, goal)
    return Update_Default_NoSubGoal(self, ai, goal)
    
end


