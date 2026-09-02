RegisterTableGoal(GOAL_DarknessDragon621100_Battle, "GOAL_DarknessDragon621100_Battle")
REGISTER_GOAL_NO_SUB_GOAL(GOAL_DarknessDragon621100_Battle, true)

Goal.Initialize = function (self, ai, goal, battleActivatedCount)
    ai:SetStringIndexedNumber("isAfterHeatUp", false)
    
end

Goal.Activate = function (self, ai, goal)
    Init_Pseudo_Global(ai, goal)
    ai:SetStringIndexedNumber("Dist_SideStep", 5)
    ai:SetStringIndexedNumber("Dist_BackStep", 5)
    ai:SetStringIndexedNumber("AddDistRun", 0.2)
    local probabilities = {}
    local acts = {}
    local paramTbls = {}
    Common_Clear_Param(probabilities, acts, paramTbls)
    local distanceEnemy = ai:GetDist(TARGET_ENE_0)
    local distanceYEnemy = ai:GetDistYSigned(TARGET_ENE_0)
    local random = ai:GetRandam_Int(1, 100)
    local paramAI_EXCEL_THINK_PARAM_TYPE__thinkattr_doAdmirer = ai:GetExcelParam(AI_EXCEL_THINK_PARAM_TYPE__thinkattr_doAdmirer)
    local eventRequest = ai:GetEventRequest()
    local hpRatioSelf = ai:GetHpRate(TARGET_SELF)
    ai:AddObserveSpecialEffectAttribute(TARGET_SELF, 5025)
    ai:AddObserveSpecialEffectAttribute(TARGET_SELF, 5026)
    ai:AddObserveSpecialEffectAttribute(TARGET_SELF, 5027)
    ai:AddObserveSpecialEffectAttribute(TARGET_SELF, 5028)
    ai:AddObserveSpecialEffectAttribute(TARGET_SELF, 5029)
    local f2_local9 = ai:IsInsideTargetRegion(TARGET_SELF, 5102880)
    if ai:HasSpecialEffectId(TARGET_SELF, 16582) then
        probabilities[25] = 8
    elseif hpRatioSelf < 0.5 and not ai:HasSpecialEffectId(TARGET_SELF, 16581) then
        goal:AddSubGoal(GOAL_COMMON_ComboTunable_SuccessAngle180, 20, 3014, TARGET_ENE_0, 999, 0, 0, 0, 0)
        return true
    elseif ai:HasSpecialEffectId(TARGET_ENE_0, 16575) then
        if ai:HasSpecialEffectId(TARGET_SELF, 16581) then
            probabilities[5] = 4
            probabilities[16] = 2
        else
            probabilities[16] = 3
        end
        probabilities[24] = 2
        probabilities[7] = 1
        if f2_local9 then
            probabilities[19] = 1
            probabilities[11] = 1
        end
    elseif ai:HasSpecialEffectId(TARGET_ENE_0, 16573) then
        probabilities[17] = 10
        probabilities[23] = 5
        probabilities[16] = 3
        if ai:HasSpecialEffectId(TARGET_SELF, 16581) then
            probabilities[5] = 3
        end
        if f2_local9 then
            probabilities[19] = 1
            probabilities[11] = 1
        end
    elseif ai:HasSpecialEffectId(TARGET_ENE_0, 16574) then
        probabilities[18] = 10
        probabilities[23] = 5
        probabilities[16] = 3
        if ai:HasSpecialEffectId(TARGET_SELF, 16581) then
            probabilities[5] = 3
        end
        if f2_local9 then
            probabilities[19] = 1
            probabilities[11] = 1
        end
    elseif ai:HasSpecialEffectId(TARGET_ENE_0, 16576) or InsideRange(ai, goal, 180, 140, -999, -4) then
        if ai:HasSpecialEffectId(TARGET_SELF, 16581) then
            probabilities[5] = 4
            probabilities[16] = 2
        else
            probabilities[16] = 3
        end
        probabilities[23] = 5
        if f2_local9 then
            probabilities[19] = 1
            probabilities[11] = 1
        end
    elseif ai:HasSpecialEffectId(TARGET_ENE_0, 16571) or InsideRange(ai, goal, 45, 90, -999, -4) then
        probabilities[14] = 6
        if ai:HasSpecialEffectId(TARGET_SELF, 16581) then
            probabilities[5] = 4
            probabilities[16] = 2
        else
            probabilities[16] = 2
        end
        probabilities[24] = 2
        if f2_local9 then
            probabilities[19] = 1
            probabilities[11] = 2
        end
    elseif ai:HasSpecialEffectId(TARGET_ENE_0, 16572) or InsideRange(ai, goal, -45, 90, -999, -4) then
        probabilities[15] = 6
        if ai:HasSpecialEffectId(TARGET_SELF, 16581) then
            probabilities[5] = 4
            probabilities[16] = 2
        else
            probabilities[16] = 2
        end
        probabilities[24] = 2
        if f2_local9 then
            probabilities[19] = 1
            probabilities[11] = 2
        end
    elseif InsideRange(ai, goal, 180, 90, -999, 999) then
        probabilities[20] = 10
    elseif InsideRange(ai, goal, 90, 90, -999, 999) then
        probabilities[20] = 10
    elseif InsideRange(ai, goal, -90, 90, -999, 999) then
        probabilities[20] = 10
    elseif distanceEnemy > 17 then
        probabilities[24] = 4
        probabilities[2] = 4
        if ai:HasSpecialEffectId(TARGET_SELF, 16581) then
            probabilities[1] = 6
            probabilities[7] = 1
            probabilities[22] = 10
        else
            probabilities[3] = 7
            probabilities[7] = 3
        end
        if f2_local9 then
            probabilities[19] = 1
        end
    elseif distanceEnemy > 8.5 then
        if ai:HasSpecialEffectId(TARGET_SELF, 16581) then
            probabilities[6] = 5
            probabilities[24] = 3
            probabilities[7] = 1
        else
            probabilities[7] = 6
            probabilities[3] = 5
            probabilities[6] = 4
            probabilities[24] = 2
        end
        if f2_local9 then
            probabilities[11] = 1
            probabilities[19] = 1
        end
    elseif distanceEnemy >= 0 or distanceEnemy >= -3 then
        probabilities[4] = 8
        probabilities[9] = 6
        probabilities[8] = 8
        probabilities[12] = 10
        probabilities[13] = 10
        if f2_local9 then
            probabilities[19] = 3
            probabilities[11] = 2
        end
        if ai:HasSpecialEffectId(TARGET_SELF, 16581) then
            probabilities[24] = 10
            probabilities[5] = 5
        else
            probabilities[24] = 5
        end
    else
        probabilities[24] = 3
        probabilities[16] = 3
        if ai:HasSpecialEffectId(TARGET_SELF, 16581) then
            probabilities[5] = 4
        end
        if f2_local9 then
            probabilities[19] = 2
            probabilities[11] = 1
        end
    end
    probabilities[1] = SetCoolTime(ai, goal, 3011, 15, probabilities[1], 0)
    probabilities[2] = SetCoolTime(ai, goal, 3010, 15, probabilities[2], 0)
    probabilities[3] = SetCoolTime(ai, goal, 3012, 20, probabilities[3], 0)
    probabilities[4] = SetCoolTime(ai, goal, 3000, 30, probabilities[4], 0)
    probabilities[5] = SetCoolTime(ai, goal, 3014, 50, probabilities[5], 0)
    probabilities[6] = SetCoolTime(ai, goal, 3008, 10, probabilities[6], 0)
    probabilities[8] = SetCoolTime(ai, goal, 3029, 30, probabilities[8], 0)
    probabilities[9] = SetCoolTime(ai, goal, 3023, 40, probabilities[9], 0)
    probabilities[12] = SetCoolTime(ai, goal, 3027, 30, probabilities[12], 0)
    probabilities[13] = SetCoolTime(ai, goal, 3024, 40, probabilities[13], 0)
    probabilities[16] = SetCoolTime(ai, goal, 3017, 40, probabilities[16], 0)
    probabilities[19] = SetCoolTime(ai, goal, 3039, 40, probabilities[19], 0)
    probabilities[22] = SetCoolTime(ai, goal, 3038, 60, probabilities[22], 0)
    probabilities[24] = SetCoolTime(ai, goal, 3002, 60, probabilities[24], 0)
    acts[1] = REGIST_FUNC(ai, goal, GOAL_621100_DarknessDragon_Act01)
    acts[2] = REGIST_FUNC(ai, goal, GOAL_621100_DarknessDragon_Act02)
    acts[3] = REGIST_FUNC(ai, goal, GOAL_621100_DarknessDragon_Act03)
    acts[4] = REGIST_FUNC(ai, goal, GOAL_621100_DarknessDragon_Act04)
    acts[5] = REGIST_FUNC(ai, goal, GOAL_621100_DarknessDragon_Act05)
    acts[6] = REGIST_FUNC(ai, goal, GOAL_621100_DarknessDragon_Act06)
    acts[7] = REGIST_FUNC(ai, goal, GOAL_621100_DarknessDragon_Act07)
    acts[8] = REGIST_FUNC(ai, goal, GOAL_621100_DarknessDragon_Act08)
    acts[9] = REGIST_FUNC(ai, goal, GOAL_621100_DarknessDragon_Act09)
    acts[10] = REGIST_FUNC(ai, goal, GOAL_621100_DarknessDragon_Act10)
    acts[11] = REGIST_FUNC(ai, goal, GOAL_621100_DarknessDragon_Act11)
    acts[12] = REGIST_FUNC(ai, goal, GOAL_621100_DarknessDragon_Act12)
    acts[13] = REGIST_FUNC(ai, goal, GOAL_621100_DarknessDragon_Act13)
    acts[14] = REGIST_FUNC(ai, goal, GOAL_621100_DarknessDragon_Act14)
    acts[15] = REGIST_FUNC(ai, goal, GOAL_621100_DarknessDragon_Act15)
    acts[16] = REGIST_FUNC(ai, goal, GOAL_621100_DarknessDragon_Act16)
    acts[17] = REGIST_FUNC(ai, goal, GOAL_621100_DarknessDragon_Act17)
    acts[18] = REGIST_FUNC(ai, goal, GOAL_621100_DarknessDragon_Act18)
    acts[19] = REGIST_FUNC(ai, goal, GOAL_621100_DarknessDragon_Act19)
    acts[20] = REGIST_FUNC(ai, goal, GOAL_621100_DarknessDragon_Act20)
    acts[22] = REGIST_FUNC(ai, goal, GOAL_621100_DarknessDragon_Act22)
    acts[23] = REGIST_FUNC(ai, goal, GOAL_621100_DarknessDragon_Act23)
    acts[24] = REGIST_FUNC(ai, goal, GOAL_621100_DarknessDragon_Act24)
    acts[25] = REGIST_FUNC(ai, goal, GOAL_621100_DarknessDragon_Act25)
    local actAfter = REGIST_FUNC(ai, goal, GOAL_621100_DarknessDragon_ActAfter_AdjustSpace)
    Common_Battle_Activate(ai, goal, probabilities, acts, actAfter, paramTbls)
    
end

function GOAL_621100_DarknessDragon_Act01(ai, goal, paramTbl)
    local stopDist = 30
    local canRunDist = 999
    local forceRunMinDist = 999
    local runProbability = 0
    local guardProbability = 0
    local walkLife = 3
    local runLife = 3
    Approach_Act_Flex(ai, goal, stopDist, canRunDist, forceRunMinDist, runProbability, guardProbability, walkLife, runLife)
    local successDistance = 999
    local turnTime = 0
    local turnFaceAngle = 0
    local random = ai:GetRandam_Int(1, 100)
    goal:AddSubGoal(GOAL_COMMON_ComboAttackTunableSpin, 30, 3011, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle, 0, 0)
    GetWellSpace_Odds = 0
    return GetWellSpace_Odds
    
end

function GOAL_621100_DarknessDragon_Act02(ai, goal, paramTbl)
    local stopDist = 30
    local canRunDist = 999
    local forceRunMinDist = 999
    local runProbability = 0
    local guardProbability = 0
    local walkLife = 3
    local runLife = 3
    Approach_Act_Flex(ai, goal, stopDist, canRunDist, forceRunMinDist, runProbability, guardProbability, walkLife, runLife)
    local successDistance = 999
    local turnTime = 0
    local turnFaceAngle = 0
    local random = ai:GetRandam_Int(1, 100)
    ai:AddObserveSpecialEffectAttribute(TARGET_SELF, 5025)
    goal:AddSubGoal(GOAL_COMMON_ComboAttackTunableSpin, 30, 3010, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle, 0, 0)
    GetWellSpace_Odds = 0
    return GetWellSpace_Odds
    
end

function GOAL_621100_DarknessDragon_Act03(ai, goal, paramTbl)
    local stopDist = 30
    local canRunDist = 999
    local forceRunMinDist = 999
    local runProbability = 0
    local guardProbability = 0
    local walkLife = 3
    local runLife = 3
    Approach_Act_Flex(ai, goal, stopDist, canRunDist, forceRunMinDist, runProbability, guardProbability, walkLife, runLife)
    local successDistance = 999
    local turnTime = 0
    local turnFaceAngle = 0
    local random = ai:GetRandam_Int(1, 100)
    ai:AddObserveSpecialEffectAttribute(TARGET_SELF, 5026)
    goal:AddSubGoal(GOAL_COMMON_ComboTunable_SuccessAngle180, 30, 3012, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle)
    GetWellSpace_Odds = 0
    return GetWellSpace_Odds
    
end

function GOAL_621100_DarknessDragon_Act04(ai, goal, paramTbl)
    local successDistance = 999
    local turnTime = 0
    local turnFaceAngle = 0
    local random = ai:GetRandam_Int(1, 100)
    ai:AddObserveSpecialEffectAttribute(TARGET_SELF, 5027)
    ai:AddObserveSpecialEffectAttribute(TARGET_SELF, 5028)
    goal:AddSubGoal(GOAL_COMMON_ComboAttackTunableSpin, 20, 3000, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle)
    GetWellSpace_Odds = 0
    return GetWellSpace_Odds
    
end

function GOAL_621100_DarknessDragon_Act05(ai, goal, paramTbl)
    local successDistance = 999
    local turnTime = 0
    local turnFaceAngle = 0
    local random = ai:GetRandam_Int(1, 100)
    goal:AddSubGoal(GOAL_COMMON_ComboAttackTunableSpin, 20, 3014, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle, 0, 0)
    GetWellSpace_Odds = 0
    return GetWellSpace_Odds
    
end

function GOAL_621100_DarknessDragon_Act06(ai, goal, paramTbl)
    local stopDist = 12
    local canRunDist = 999
    local forceRunMinDist = 999
    local runProbability = 0
    local guardProbability = 0
    local walkLife = 3
    local runLife = 3
    Approach_Act_Flex(ai, goal, stopDist, canRunDist, forceRunMinDist, runProbability, guardProbability, walkLife, runLife)
    local successDistance = 999
    local turnTime = 0
    local turnFaceAngle = 0
    local random = ai:GetRandam_Int(1, 100)
    goal:AddSubGoal(GOAL_COMMON_ComboTunable_SuccessAngle180, 10, 3008, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle, 0, 0)
    if ai:HasSpecialEffectId(TARGET_SELF, 16581) and not ai:HasSpecialEffectId(TARGET_SELF, 16585) then
        goal:AddSubGoal(GOAL_COMMON_ComboFinal, 10, 3009, TARGET_ENE_0, successDistance)
    end
    GetWellSpace_Odds = 0
    return GetWellSpace_Odds
    
end

function GOAL_621100_DarknessDragon_Act07(ai, goal, paramTbl)
    local stopDist = 30
    local canRunDist = 999
    local forceRunMinDist = 999
    local runProbability = 0
    local guardProbability = 0
    local walkLife = 3
    local runLife = 3
    Approach_Act_Flex(ai, goal, stopDist, canRunDist, forceRunMinDist, runProbability, guardProbability, walkLife, runLife)
    local successDistance = 999
    local turnTime = 0
    local turnFaceAngle = 0
    local random = ai:GetRandam_Int(1, 100)
    goal:AddSubGoal(GOAL_COMMON_ComboAttackTunableSpin, 20, 3015, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle, 0, 0)
    GetWellSpace_Odds = 0
    return GetWellSpace_Odds
    
end

function GOAL_621100_DarknessDragon_Act08(ai, goal, paramTbl)
    local stopDist = 1.5
    local canRunDist = 999
    local forceRunMinDist = 999
    local runProbability = 0
    local guardProbability = 0
    local walkLife = 3
    local runLife = 3
    Approach_Act_Flex(ai, goal, stopDist, canRunDist, forceRunMinDist, runProbability, guardProbability, walkLife, runLife)
    local successDistance = 999
    local turnTime = 0
    local turnFaceAngle = 0
    local random = ai:GetRandam_Int(1, 100)
    goal:AddSubGoal(GOAL_COMMON_ComboTunable_SuccessAngle180, 10, 3029, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle)
    ai:AddObserveSpecialEffectAttribute(TARGET_SELF, 5028)
    if ai:HasSpecialEffectId(TARGET_SELF, 16581) and not ai:HasSpecialEffectId(TARGET_SELF, 16585) then
        goal:AddSubGoal(GOAL_COMMON_ComboFinal, 20, 3009, TARGET_ENE_0, successDistance)
    else
        goal:AddSubGoal(GOAL_COMMON_ComboFinal, 10, 3026, TARGET_ENE_0, successDistance)
    end
    GetWellSpace_Odds = 0
    return GetWellSpace_Odds
    
end

function GOAL_621100_DarknessDragon_Act09(ai, goal, paramTbl)
    local successDistance = 999
    local turnTime = 0
    local turnFaceAngle = 0
    local random = ai:GetRandam_Int(1, 100)
    goal:AddSubGoal(GOAL_COMMON_ComboTunable_SuccessAngle180, 20, 3023, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle)
    goal:AddSubGoal(GOAL_COMMON_ComboFinal, 80, 3006, TARGET_ENE_0, successDistance)
    GetWellSpace_Odds = 0
    return GetWellSpace_Odds
    
end

function GOAL_621100_DarknessDragon_Act10(ai, goal, paramTbl)
    local successDistance = 999
    local turnTime = 0
    local turnFaceAngle = 0
    local random = ai:GetRandam_Int(1, 100)
    goal:AddSubGoal(GOAL_COMMON_ComboAttackTunableSpin, 10, 3023, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle)
    GetWellSpace_Odds = 0
    return GetWellSpace_Odds
    
end

function GOAL_621100_DarknessDragon_Act11(ai, goal, paramTbl)
    local successDistance = 999
    local turnTime = 0
    local turnFaceAngle = 0
    local random = ai:GetRandam_Int(1, 100)
    local angleToEnemy = ai:GetRelativeAngleFromTarget(TARGET_ENE_0)
    if angleToEnemy >= 5 then
        goal:AddSubGoal(GOAL_COMMON_ComboAttackTunableSpin, 10, 3033, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle)
    else
        goal:AddSubGoal(GOAL_COMMON_ComboAttackTunableSpin, 10, 3034, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle)
    end
    GetWellSpace_Odds = 0
    return GetWellSpace_Odds
    
end

function GOAL_621100_DarknessDragon_Act12(ai, goal, paramTbl)
    local stopDist = 4
    local canRunDist = 999
    local forceRunMinDist = 999
    local runProbability = 0
    local guardProbability = 0
    local walkLife = 3
    local runLife = 3
    Approach_Act_Flex(ai, goal, stopDist, canRunDist, forceRunMinDist, runProbability, guardProbability, walkLife, runLife)
    local successDistance = 999
    local turnTime = 0
    local turnFaceAngle = 0
    local random = ai:GetRandam_Int(1, 100)
    goal:AddSubGoal(GOAL_COMMON_ComboTunable_SuccessAngle180, 10, 3027, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle, 0, 0)
    ai:AddObserveSpecialEffectAttribute(TARGET_SELF, 5028)
    if ai:HasSpecialEffectId(TARGET_SELF, 16581) and not ai:HasSpecialEffectId(TARGET_SELF, 16585) and random > 30 then
        goal:AddSubGoal(GOAL_COMMON_ComboFinal, 10, 3009, TARGET_ENE_0, successDistance)
    elseif random > 30 then
        ai:AddObserveSpecialEffectAttribute(TARGET_SELF, 5029)
        goal:AddSubGoal(GOAL_COMMON_ComboFinal, 20, 3032, TARGET_ENE_0, successDistance)
    else
        goal:AddSubGoal(GOAL_COMMON_ComboFinal, 10, 3026, TARGET_ENE_0, successDistance)
    end
    GetWellSpace_Odds = 0
    return GetWellSpace_Odds
    
end

function GOAL_621100_DarknessDragon_Act13(ai, goal, paramTbl)
    local stopDist = 1.5
    local canRunDist = 999
    local forceRunMinDist = 999
    local runProbability = 0
    local guardProbability = 0
    local walkLife = 3
    local runLife = 3
    Approach_Act_Flex(ai, goal, stopDist, canRunDist, forceRunMinDist, runProbability, guardProbability, walkLife, runLife)
    local successDistance = 999
    local turnTime = 0
    local turnFaceAngle = 0
    local random = ai:GetRandam_Int(1, 100)
    ai:AddObserveSpecialEffectAttribute(TARGET_SELF, 5029)
    goal:AddSubGoal(GOAL_COMMON_ComboTunable_SuccessAngle180, 10, 3024, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle)
    goal:AddSubGoal(GOAL_COMMON_ComboRepeat, 10, 3025, TARGET_ENE_0, successDistance)
    goal:AddSubGoal(GOAL_COMMON_ComboFinal, 20, 3031, TARGET_ENE_0, successDistance)
    GetWellSpace_Odds = 0
    return GetWellSpace_Odds
    
end

function GOAL_621100_DarknessDragon_Act14(ai, goal, paramTbl)
    local successDistance = 999
    local turnTime = 0
    local turnFaceAngle = 0
    local random = ai:GetRandam_Int(1, 100)
    goal:AddSubGoal(GOAL_COMMON_ComboTunable_SuccessAngle180, 10, 3021, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle)
    goal:AddSubGoal(GOAL_COMMON_ComboFinal, 10, 3019, TARGET_ENE_0, successDistance)
    GetWellSpace_Odds = 0
    return GetWellSpace_Odds
    
end

function GOAL_621100_DarknessDragon_Act15(ai, goal, paramTbl)
    local successDistance = 999
    local turnTime = 0
    local turnFaceAngle = 0
    local random = ai:GetRandam_Int(1, 100)
    goal:AddSubGoal(GOAL_COMMON_ComboTunable_SuccessAngle180, 10, 3022, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle)
    goal:AddSubGoal(GOAL_COMMON_ComboFinal, 10, 3020, TARGET_ENE_0, successDistance)
    GetWellSpace_Odds = 0
    return GetWellSpace_Odds
    
end

function GOAL_621100_DarknessDragon_Act16(ai, goal, paramTbl)
    local successDistance = 999
    local turnTime = 0
    local turnFaceAngle = 0
    local random = ai:GetRandam_Int(1, 100)
    goal:AddSubGoal(GOAL_COMMON_ComboAttackTunableSpin, 20, 3017, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle, 0, 0)
    GetWellSpace_Odds = 0
    return GetWellSpace_Odds
    
end

function GOAL_621100_DarknessDragon_Act17(ai, goal, paramTbl)
    local successDistance = 999
    local turnTime = 0
    local turnFaceAngle = 0
    local random = ai:GetRandam_Int(1, 100)
    goal:AddSubGoal(GOAL_COMMON_ComboAttackTunableSpin, 10, 3036, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle)
    GetWellSpace_Odds = 0
    return GetWellSpace_Odds
    
end

function GOAL_621100_DarknessDragon_Act18(ai, goal, paramTbl)
    local successDistance = 999
    local turnTime = 0
    local turnFaceAngle = 0
    local random = ai:GetRandam_Int(1, 100)
    goal:AddSubGoal(GOAL_COMMON_ComboAttackTunableSpin, 10, 3037, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle)
    GetWellSpace_Odds = 0
    return GetWellSpace_Odds
    
end

function GOAL_621100_DarknessDragon_Act19(ai, goal, paramTbl)
    local successDistance = 999
    local turnTime = 0
    local turnFaceAngle = 0
    local random = ai:GetRandam_Int(1, 100)
    goal:AddSubGoal(GOAL_COMMON_ComboAttackTunableSpin, 30, 3039, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle)
    GetWellSpace_Odds = 0
    return GetWellSpace_Odds
    
end

function GOAL_621100_DarknessDragon_Act20(ai, goal, paramTbl)
    goal:AddSubGoal(GOAL_COMMON_Turn, 3, TARGET_ENE_0, 20)
    GetWellSpace_Odds = 0
    return GetWellSpace_Odds
    
end

function GOAL_621100_DarknessDragon_Act21(ai, goal, paramTbl)
    goal:AddSubGoal(GOAL_COMMON_KeepDist, ai:GetRandam_Float(2.5, 4), TARGET_ENE_0, 1, 16, TARGET_ENE_0, true, -1)
    
end

function GOAL_621100_DarknessDragon_Act22(ai, goal, paramTbl)
    local successDistance = 999
    local turnTime = 0
    local turnFaceAngle = 0
    local random = ai:GetRandam_Int(1, 100)
    goal:AddSubGoal(GOAL_COMMON_ComboAttackTunableSpin, 10, 3034, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle)
    goal:AddSubGoal(GOAL_COMMON_ComboAttackTunableSpin, 30, 3038, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle)
    GetWellSpace_Odds = 0
    return GetWellSpace_Odds
    
end

function GOAL_621100_DarknessDragon_Act23(ai, goal, paramTbl)
    local successDistance = 999
    local turnTime = 0
    local turnFaceAngle = 0
    local random = ai:GetRandam_Int(1, 100)
    goal:AddSubGoal(GOAL_COMMON_ComboAttackTunableSpin, 30, 3016, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle)
    GetWellSpace_Odds = 0
    return GetWellSpace_Odds
    
end

function GOAL_621100_DarknessDragon_Act24(ai, goal, paramTbl)
    local successDistance = 999
    local turnTime = 0
    local turnFaceAngle = 0
    local random = ai:GetRandam_Int(1, 100)
    goal:AddSubGoal(GOAL_COMMON_ComboAttackTunableSpin, 40, 3002, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle)
    if not ai:HasSpecialEffectId(TARGET_SELF, 16582) and ai:HasSpecialEffectId(TARGET_SELF, 16581) then
        goal:AddSubGoal(GOAL_COMMON_ComboFinal, 40, 3002, TARGET_ENE_0, successDistance)
    end
    GetWellSpace_Odds = 0
    return GetWellSpace_Odds
    
end

function GOAL_621100_DarknessDragon_Act25(ai, goal, paramTbl)
    local successDistance = 999
    local turnTime = 0
    local turnFaceAngle = 0
    local random = ai:GetRandam_Int(1, 100)
    goal:AddSubGoal(GOAL_COMMON_ComboAttackTunableSpin, 40, 3002, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle)
    GetWellSpace_Odds = 0
    return GetWellSpace_Odds
    
end

Goal.Update = function (self, ai, goal)
    return Update_Default_NoSubGoal(self, ai, goal)
    
end

Goal.Terminate = function (self, ai, goal)
    
end

Goal.Interrupt = function (self, ai, goal)
    local distanceEnemy = ai:GetDist(TARGET_ENE_0)
    local distanceYEnemy = ai:GetDistYSigned(TARGET_ENE_0)
    local random = ai:GetRandam_Int(1, 100)
    local random_2 = ai:GetRandam_Int(1, 100)
    local random_3 = ai:GetRandam_Int(1, 100)
    if ai:IsInterupt(INTERUPT_ActivateSpecialEffect) then
        if ai:GetSpecialEffectActivateInterruptType(0) == 5025 and distanceEnemy >= 5 and distanceEnemy <= 24 then
            goal:ClearSubGoal()
            if random > 50 then
                goal:AddSubGoal(GOAL_COMMON_ComboFinal, 30, 3030, TARGET_ENE_0, 999, 0, 0)
            else
                goal:AddSubGoal(GOAL_COMMON_ComboFinal, 30, 3031, TARGET_ENE_0, 999, 0, 0)
            end
            return true
        end
        if ai:GetSpecialEffectActivateInterruptType(0) == 5026 and distanceEnemy <= 22 then
            goal:ClearSubGoal()
            goal:AddSubGoal(GOAL_COMMON_ComboFinal, 30, 3013, TARGET_ENE_0, 999, 0, 0)
            return true
        end
        if ai:GetSpecialEffectActivateInterruptType(0) == 5027 and distanceEnemy <= -5 then
            goal:ClearSubGoal()
            goal:AddSubGoal(GOAL_COMMON_ComboFinal, 30, 3001, TARGET_ENE_0, 999, 0, 0)
            return true
        end
        if ai:GetSpecialEffectActivateInterruptType(0) == 5028 and distanceEnemy <= -5 then
            goal:ClearSubGoal()
            goal:AddSubGoal(GOAL_COMMON_ComboFinal, 30, 3018, TARGET_ENE_0, 999, 0, 0)
            return true
        end
        if ai:GetSpecialEffectActivateInterruptType(0) == 5029 and distanceEnemy >= 15 then
            goal:ClearSubGoal()
            goal:AddSubGoal(GOAL_COMMON_ComboFinal, 20, 3015, TARGET_ENE_0, 999, 0, 0)
            return true
        end
    end
    return false
    
end

function GOAL_621100_DarknessDragon_ActAfter_AdjustSpace(ai, goal, paramTbl)
    goal:AddSubGoal(GOAL_DarknessDragon621100_Battle_AfterAttackAct, 10)
    
end

RegisterTableGoal(GOAL_DarknessDragon621100_Battle_AfterAttackAct, "GOAL_DarknessDragon621100_Battle_AfterAttackAct")
REGISTER_GOAL_NO_SUB_GOAL(GOAL_DarknessDragon621100_Battle_AfterAttackAct, true)

Goal.Activate = function (self, ai, goal)
    local distanceEnemy = ai:GetDist(TARGET_ENE_0)
    local hpRatioSelf = ai:GetHpRate(TARGET_SELF)
    local weightArray = {}
    if ai:IsInsideTarget(TARGET_ENE_0, AI_DIR_TYPE_B, 180) and distanceEnemy <= 3 then
    elseif distanceEnemy >= 7 then
    elseif distanceEnemy >= 5 then
    elseif distanceEnemy >= 3 then
    else
    end
    if SelectOddsIndex(ai, weightArray) == 1 then
    end
    
end

Goal.Update = function (self, ai, goal)
    return Update_Default_NoSubGoal(self, ai, goal)
    
end


