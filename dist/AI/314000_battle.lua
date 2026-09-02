local f0_local0 = {3204300, 3204310, 3204311, 3204312, 3204313, 3204314, 3204315, 3204316, 3204317, 3204318, 3204319, 3204320, 3204321, 3204322, 3204323, 3204324, 3204325, 3204328, 3204329}
RegisterTableGoal(GOAL_Hellkite_314000_Battle, "Hellkite_314000_Battle")

Goal.Initialize = function (self, ai, goal, battleActivatedCount)
    
end

Goal.Activate = function (self, ai, goal)
    Init_Pseudo_Global(ai, goal)
    ai:SetStringIndexedNumber("Dist_SideStep", 3)
    ai:SetStringIndexedNumber("Dist_BackStep", 3)
    local probabilities = {}
    local acts = {}
    local paramTbls = {}
    Common_Clear_Param(probabilities, acts, paramTbls)
    local distanceEnemy = ai:GetDist(TARGET_ENE_0)
    local eventRequest = ai:GetEventRequest()
    if InsideRange(ai, goal, 180, 180, 3, 10) and ai:GetRandam_Int(1, 100) <= 75 then
        probabilities[7] = 100
    elseif ai:IsInsideTargetRegion(TARGET_ENE_0, 3204350) or ai:IsInsideTargetRegion(TARGET_ENE_0, 3204351) or ai:IsInsideTargetRegion(TARGET_ENE_0, 3204352) or ai:IsInsideTargetRegion(TARGET_ENE_0, 3204353) or ai:IsInsideTargetRegion(TARGET_ENE_0, 3204354) then
        if ai:IsInsideTarget(TARGET_ENE_0, AI_DIR_TYPE_B, 90) then
            probabilities[20] = 100
        elseif distanceEnemy >= 20 then
            probabilities[1] = 100
            probabilities[2] = 150
            probabilities[3] = 30
            probabilities[4] = 10
            probabilities[5] = 10
            probabilities[6] = 10
            probabilities[7] = 0
            probabilities[8] = 60
            probabilities[9] = 0
        elseif distanceEnemy >= 13 then
            probabilities[1] = 40
            probabilities[2] = 80
            probabilities[3] = 50
            probabilities[4] = 20
            probabilities[5] = 20
            probabilities[6] = 20
            probabilities[7] = 0
            probabilities[8] = 80
            probabilities[9] = 50
        elseif distanceEnemy >= 9 then
            probabilities[1] = 10
            probabilities[2] = 30
            probabilities[3] = 150
            probabilities[4] = 30
            probabilities[5] = 30
            probabilities[6] = 30
            probabilities[7] = 0
            probabilities[8] = 80
            probabilities[9] = 80
        else
            probabilities[1] = 0
            probabilities[2] = 0
            probabilities[3] = 0
            probabilities[4] = 100
            probabilities[5] = 100
            probabilities[6] = 100
            probabilities[7] = 100
            probabilities[8] = 0
            probabilities[9] = 80
        end
    elseif ai:IsInsideTargetRegion(TARGET_ENE_0, 3204355) then
        if not ai:IsInsideTargetRegion(TARGET_SELF, 3204354) then
            probabilities[21] = 100
        elseif distanceEnemy <= 12 then
            probabilities[4] = 30
            probabilities[5] = 20
            probabilities[6] = 20
            probabilities[9] = 40
        elseif distanceEnemy <= 20 then
            probabilities[1] = 10
            probabilities[2] = 40
            probabilities[3] = 50
            probabilities[4] = 10
            probabilities[9] = 30
        elseif ai:IsInsideTargetRegion(TARGET_SELF, 3204354) then
            probabilities[41] = 100
        end
    elseif ai:IsInsideTargetRegion(TARGET_ENE_0, 3204358) then
        if ai:IsInsideTargetRegion(TARGET_SELF, 3204340) then
            probabilities[23] = 100
        else
            probabilities[24] = 100
        end
    elseif ai:IsInsideTargetRegion(TARGET_ENE_0, 3204356) then
        if ai:IsInsideTargetRegion(TARGET_SELF, 3204350) or ai:IsInsideTargetRegion(TARGET_SELF, 3204351) or ai:IsInsideTargetRegion(TARGET_SELF, 3204352) or ai:IsInsideTargetRegion(TARGET_SELF, 3204353) then
            probabilities[21] = 100
        elseif ai:IsInsideTargetRegion(TARGET_SELF, 3204355) or ai:IsInsideTargetRegion(TARGET_SELF, 3204356) then
            probabilities[41] = 100
        elseif ai:IsInsideTargetRegion(TARGET_SELF, 3204354) then
            if ai:IsInsideTargetRegion(TARGET_SELF, 3204309) then
                probabilities[37] = 100
            else
                local angleToEnemy = ai:GetToTargetAngle(TARGET_ENE_0)
                if math.abs(angleToEnemy) <= 45 then
                    probabilities[37] = 100
                else
                    probabilities[21] = 100
                end
            end
        elseif ai:IsInsideTargetRegion(TARGET_SELF, 3204357) then
            probabilities[21] = 50
            probabilities[37] = 50
        else
            probabilities[21] = 100
        end
    elseif ai:IsInsideTargetRegion(TARGET_ENE_0, 3204357) then
        if ai:IsInsideTargetRegion(TARGET_SELF, 3204309) then
            if distanceEnemy <= 9 then
                probabilities[1] = 0
                probabilities[2] = 0
                probabilities[3] = 0
                probabilities[4] = 100
                probabilities[5] = 100
                probabilities[6] = 100
                probabilities[7] = 50
                probabilities[9] = 80
            elseif distanceEnemy <= 15 then
                probabilities[1] = 10
                probabilities[2] = 50
                probabilities[3] = 160
                probabilities[4] = 10
                probabilities[5] = 10
                probabilities[6] = 10
                probabilities[9] = 100
            elseif distanceEnemy <= 20 then
                probabilities[1] = 70
                probabilities[2] = 150
                probabilities[3] = 80
                probabilities[4] = 0
                probabilities[5] = 0
                probabilities[6] = 0
                probabilities[9] = 30
            else
                probabilities[29] = 100
            end
        else
            probabilities[21] = 100
        end
    elseif ai:IsInsideTargetRegion(TARGET_ENE_0, 3204359) or ai:IsInsideTargetRegion(TARGET_ENE_0, 3204360) then
        if ai:IsInsideTargetRegion(TARGET_SELF, 3204300) or ai:IsInsideTargetRegion(TARGET_SELF, 3204353) or ai:IsInsideTargetRegion(TARGET_SELF, 3204354) or ai:IsInsideTargetRegion(TARGET_SELF, 3204355) then
            probabilities[40] = 100
        else
            probabilities[21] = 100
        end
    elseif ai:IsInsideTargetRegion(TARGET_ENE_0, 3204362) or ai:IsInsideTargetRegion(TARGET_ENE_0, 3204363) or ai:IsInsideTargetRegion(TARGET_ENE_0, 3204364) then
        probabilities[50] = 100
    elseif ai:IsInsideTargetRegion(TARGET_SELF, 3204340) then
        probabilities[22] = 100
    else
        probabilities[46] = 100
    end
    probabilities[1] = SetCoolTime(ai, goal, 3000, 10, probabilities[1], 1)
    probabilities[24] = SetCoolTime(ai, goal, 3000, 10, probabilities[24], 1)
    probabilities[26] = SetCoolTime(ai, goal, 3000, 10, probabilities[26], 1)
    probabilities[29] = SetCoolTime(ai, goal, 3000, 10, probabilities[29], 1)
    probabilities[32] = SetCoolTime(ai, goal, 3000, 10, probabilities[32], 1)
    probabilities[35] = SetCoolTime(ai, goal, 3000, 10, probabilities[35], 1)
    probabilities[38] = SetCoolTime(ai, goal, 3000, 10, probabilities[38], 1)
    probabilities[2] = SetCoolTime(ai, goal, 3001, 10, probabilities[2], 1)
    probabilities[25] = SetCoolTime(ai, goal, 3001, 10, probabilities[25], 1)
    probabilities[27] = SetCoolTime(ai, goal, 3001, 10, probabilities[27], 1)
    probabilities[30] = SetCoolTime(ai, goal, 3001, 10, probabilities[30], 1)
    probabilities[33] = SetCoolTime(ai, goal, 3001, 10, probabilities[33], 1)
    probabilities[36] = SetCoolTime(ai, goal, 3001, 10, probabilities[36], 1)
    probabilities[39] = SetCoolTime(ai, goal, 3001, 10, probabilities[39], 1)
    probabilities[9] = SetCoolTime(ai, goal, 3008, 15, probabilities[9], 1)
    acts[1] = REGIST_FUNC(ai, goal, Hellkite_314000_Act01)
    acts[2] = REGIST_FUNC(ai, goal, Hellkite_314000_Act02)
    acts[3] = REGIST_FUNC(ai, goal, Hellkite_314000_Act03)
    acts[4] = REGIST_FUNC(ai, goal, Hellkite_314000_Act04)
    acts[5] = REGIST_FUNC(ai, goal, Hellkite_314000_Act05)
    acts[6] = REGIST_FUNC(ai, goal, Hellkite_314000_Act06)
    acts[7] = REGIST_FUNC(ai, goal, Hellkite_314000_Act07)
    acts[8] = REGIST_FUNC(ai, goal, Hellkite_314000_Act08)
    acts[9] = REGIST_FUNC(ai, goal, Hellkite_314000_Act09)
    acts[10] = REGIST_FUNC(ai, goal, Hellkite_314000_Act10)
    acts[11] = REGIST_FUNC(ai, goal, Hellkite_314000_Act11)
    acts[12] = REGIST_FUNC(ai, goal, Hellkite_314000_Act12)
    acts[13] = REGIST_FUNC(ai, goal, Hellkite_314000_Act13)
    acts[14] = REGIST_FUNC(ai, goal, Hellkite_314000_Act14)
    acts[20] = REGIST_FUNC(ai, goal, Hellkite_314000_Act20)
    acts[21] = REGIST_FUNC(ai, goal, Hellkite_314000_Act21)
    acts[22] = REGIST_FUNC(ai, goal, Hellkite_314000_Act22)
    acts[23] = REGIST_FUNC(ai, goal, Hellkite_314000_Act23)
    acts[24] = REGIST_FUNC(ai, goal, Hellkite_314000_Act24)
    acts[25] = REGIST_FUNC(ai, goal, Hellkite_314000_Act25)
    acts[26] = REGIST_FUNC(ai, goal, Hellkite_314000_Act26)
    acts[27] = REGIST_FUNC(ai, goal, Hellkite_314000_Act27)
    acts[28] = REGIST_FUNC(ai, goal, Hellkite_314000_Act28)
    acts[29] = REGIST_FUNC(ai, goal, Hellkite_314000_Act29)
    acts[30] = REGIST_FUNC(ai, goal, Hellkite_314000_Act30)
    acts[31] = REGIST_FUNC(ai, goal, Hellkite_314000_Act31)
    acts[32] = REGIST_FUNC(ai, goal, Hellkite_314000_Act32)
    acts[33] = REGIST_FUNC(ai, goal, Hellkite_314000_Act33)
    acts[34] = REGIST_FUNC(ai, goal, Hellkite_314000_Act34)
    acts[35] = REGIST_FUNC(ai, goal, Hellkite_314000_Act35)
    acts[36] = REGIST_FUNC(ai, goal, Hellkite_314000_Act36)
    acts[37] = REGIST_FUNC(ai, goal, Hellkite_314000_Act37)
    acts[38] = REGIST_FUNC(ai, goal, Hellkite_314000_Act38)
    acts[39] = REGIST_FUNC(ai, goal, Hellkite_314000_Act39)
    acts[40] = REGIST_FUNC(ai, goal, Hellkite_314000_Act40)
    acts[41] = REGIST_FUNC(ai, goal, Hellkite_314000_Act41)
    acts[42] = REGIST_FUNC(ai, goal, Hellkite_314000_Act42)
    acts[43] = REGIST_FUNC(ai, goal, Hellkite_314000_Act43)
    acts[44] = REGIST_FUNC(ai, goal, Hellkite_314000_Act44)
    acts[45] = REGIST_FUNC(ai, goal, Hellkite_314000_Act45)
    acts[46] = REGIST_FUNC(ai, goal, Hellkite_314000_Act46)
    acts[50] = REGIST_FUNC(ai, goal, Hellkite_314000_Act50)
    local actAfter = REGIST_FUNC(ai, goal, Hellkite_314000_ActAfter_AdjustSpace)
    Common_Battle_Activate(ai, goal, probabilities, acts, actAfter, paramTbls)
    
end

function Hellkite_314000_Act01(ai, goal, paramTbl)
    local stopDist = 52.8 - ai:GetMapHitRadius(TARGET_SELF) + 50
    local canRunDist = 52.8 - ai:GetMapHitRadius(TARGET_SELF) + 50
    local forceRunMinDist = 52.8 - ai:GetMapHitRadius(TARGET_SELF) + 50
    local runProbability = 0
    local guardProbability = 0
    local walkLife = 5
    local runLife = 5
    Approach_Act_Flex(ai, goal, stopDist, canRunDist, forceRunMinDist, runProbability, guardProbability, walkLife, runLife)
    local animationId = 3000
    local f3_local8 = 52.8 - ai:GetMapHitRadius(TARGET_SELF) + 1
    local successDistance = 999 - ai:GetMapHitRadius(TARGET_SELF)
    local turnTime = 1.5
    local turnFaceAngle = 20
    local random = ai:GetRandam_Int(1, 100)
    goal:AddSubGoal(GOAL_COMMON_AttackTunableSpin, 10, animationId, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle, 0, 0)
    return 100
    
end

function Hellkite_314000_Act02(ai, goal, paramTbl)
    local stopDist = 27.8 - ai:GetMapHitRadius(TARGET_SELF) + 50
    local canRunDist = 27.8 - ai:GetMapHitRadius(TARGET_SELF) + 50
    local forceRunMinDist = 27.8 - ai:GetMapHitRadius(TARGET_SELF) + 50
    local runProbability = 0
    local guardProbability = 0
    local walkLife = 5
    local runLife = 5
    Approach_Act_Flex(ai, goal, stopDist, canRunDist, forceRunMinDist, runProbability, guardProbability, walkLife, runLife)
    local animationId = 3001
    local f4_local8 = 27.8 - ai:GetMapHitRadius(TARGET_SELF) + 1
    local successDistance = 999 - ai:GetMapHitRadius(TARGET_SELF)
    local turnTime = 1.5
    local turnFaceAngle = 20
    local random = ai:GetRandam_Int(1, 100)
    goal:AddSubGoal(GOAL_COMMON_AttackTunableSpin, 10, animationId, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle, 0, 0)
    return 100
    
end

function Hellkite_314000_Act03(ai, goal, paramTbl)
    local stopDist = 16.3 - ai:GetMapHitRadius(TARGET_SELF) + 50
    local canRunDist = 16.3 - ai:GetMapHitRadius(TARGET_SELF) + 0.9
    local forceRunMinDist = 16.3 - ai:GetMapHitRadius(TARGET_SELF) + 4
    local runProbability = 0
    local guardProbability = 0
    local walkLife = 5
    local runLife = 5
    Approach_Act_Flex(ai, goal, stopDist, canRunDist, forceRunMinDist, runProbability, guardProbability, walkLife, runLife)
    local animationId = 3002
    local f5_local8 = 16.3 - ai:GetMapHitRadius(TARGET_SELF) + 1
    local successDistance = 999 - ai:GetMapHitRadius(TARGET_SELF)
    local turnTime = 1.5
    local turnFaceAngle = 20
    local random = ai:GetRandam_Int(1, 100)
    goal:AddSubGoal(GOAL_COMMON_AttackTunableSpin, 10, animationId, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle, 0, 0)
    return 100
    
end

function Hellkite_314000_Act04(ai, goal, paramTbl)
    local stopDist = 8.8 - ai:GetMapHitRadius(TARGET_SELF) + 50
    local canRunDist = 8.8 - ai:GetMapHitRadius(TARGET_SELF) + 50
    local forceRunMinDist = 8.8 - ai:GetMapHitRadius(TARGET_SELF) + 50
    local runProbability = 0
    local guardProbability = 0
    local walkLife = 5
    local runLife = 5
    Approach_Act_Flex(ai, goal, stopDist, canRunDist, forceRunMinDist, runProbability, guardProbability, walkLife, runLife)
    local animationId = 3003
    local f6_local8 = 8.8 - ai:GetMapHitRadius(TARGET_SELF) + 1
    local successDistance = 999 - ai:GetMapHitRadius(TARGET_SELF)
    local turnTime = 1.5
    local turnFaceAngle = 20
    local random = ai:GetRandam_Int(1, 100)
    goal:AddSubGoal(GOAL_COMMON_AttackTunableSpin, 10, animationId, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle, 0, 0)
    return 100
    
end

function Hellkite_314000_Act05(ai, goal, paramTbl)
    local stopDist = 6.3 - ai:GetMapHitRadius(TARGET_SELF) + 50
    local canRunDist = 6.3 - ai:GetMapHitRadius(TARGET_SELF) + 50
    local forceRunMinDist = 6.3 - ai:GetMapHitRadius(TARGET_SELF) + 50
    local runProbability = 0
    local guardProbability = 0
    local walkLife = 5
    local runLife = 5
    Approach_Act_Flex(ai, goal, stopDist, canRunDist, forceRunMinDist, runProbability, guardProbability, walkLife, runLife)
    local animationId = 3004
    local f7_local8 = 6.3 - ai:GetMapHitRadius(TARGET_SELF) + 1
    local successDistance = 999 - ai:GetMapHitRadius(TARGET_SELF)
    local turnTime = 1.5
    local turnFaceAngle = 20
    local random = ai:GetRandam_Int(1, 100)
    goal:AddSubGoal(GOAL_COMMON_AttackTunableSpin, 10, animationId, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle, 0, 0)
    return 100
    
end

function Hellkite_314000_Act06(ai, goal, paramTbl)
    local stopDist = 6.3 - ai:GetMapHitRadius(TARGET_SELF) + 50
    local canRunDist = 6.3 - ai:GetMapHitRadius(TARGET_SELF) + 50
    local forceRunMinDist = 6.3 - ai:GetMapHitRadius(TARGET_SELF) + 50
    local runProbability = 0
    local guardProbability = 0
    local walkLife = 5
    local runLife = 5
    Approach_Act_Flex(ai, goal, stopDist, canRunDist, forceRunMinDist, runProbability, guardProbability, walkLife, runLife)
    local animationId = 3005
    local f8_local8 = 6.3 - ai:GetMapHitRadius(TARGET_SELF) + 1
    local successDistance = 999 - ai:GetMapHitRadius(TARGET_SELF)
    local turnTime = 1.5
    local turnFaceAngle = 20
    local random = ai:GetRandam_Int(1, 100)
    goal:AddSubGoal(GOAL_COMMON_AttackTunableSpin, 10, animationId, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle, 0, 0)
    return 100
    
end

function Hellkite_314000_Act07(ai, goal, paramTbl)
    local animationId = 3006
    local successDistance = 999 - ai:GetMapHitRadius(TARGET_SELF)
    local angleUp = 0
    local angleDown = 0
    goal:AddSubGoal(GOAL_COMMON_NonspinningAttack, 10, animationId, TARGET_ENE_0, successDistance, angleUp, angleDown)
    return 100
    
end

function Hellkite_314000_Act08(ai, goal, paramTbl)
    local stopDist = 26.8 - ai:GetMapHitRadius(TARGET_SELF) + 50
    local canRunDist = 26.8 - ai:GetMapHitRadius(TARGET_SELF) + 0.9
    local forceRunMinDist = 26.8 - ai:GetMapHitRadius(TARGET_SELF) + 4
    local runProbability = 0
    local guardProbability = 0
    local walkLife = 5
    local runLife = 5
    Approach_Act_Flex(ai, goal, stopDist, canRunDist, forceRunMinDist, runProbability, guardProbability, walkLife, runLife)
    local animationId = 3007
    local f10_local8 = 26.8 - ai:GetMapHitRadius(TARGET_SELF) + 1
    local successDistance = 999 - ai:GetMapHitRadius(TARGET_SELF)
    local turnTime = 1.5
    local turnFaceAngle = 20
    local random = ai:GetRandam_Int(1, 100)
    goal:AddSubGoal(GOAL_COMMON_AttackTunableSpin, 10, animationId, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle, 0, 0)
    return 100
    
end

function Hellkite_314000_Act09(ai, goal, paramTbl)
    local stopDist = 10.8 - ai:GetMapHitRadius(TARGET_SELF) + 50
    local canRunDist = 10.8 - ai:GetMapHitRadius(TARGET_SELF) + 0.9
    local forceRunMinDist = 10.8 - ai:GetMapHitRadius(TARGET_SELF) + 4
    local runProbability = 0
    local guardProbability = 0
    local walkLife = 5
    local runLife = 5
    Approach_Act_Flex(ai, goal, stopDist, canRunDist, forceRunMinDist, runProbability, guardProbability, walkLife, runLife)
    local animationId = 3008
    local f11_local8 = 10.8 - ai:GetMapHitRadius(TARGET_SELF) + 1
    local successDistance = 999 - ai:GetMapHitRadius(TARGET_SELF)
    local turnTime = 1.5
    local turnFaceAngle = 20
    local random = ai:GetRandam_Int(1, 100)
    goal:AddSubGoal(GOAL_COMMON_AttackTunableSpin, 10, animationId, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle, 0, 0)
    return 100
    
end

function Hellkite_314000_Act10(ai, goal, paramTbl)
    local animationId = 3009
    local successDistance = 999 - ai:GetMapHitRadius(TARGET_SELF)
    local angleUp = 0
    local angleDown = 0
    goal:AddSubGoal(GOAL_COMMON_NonspinningAttack, 10, animationId, TARGET_ENE_0, successDistance, angleUp, angleDown)
    return 100
    
end

function Hellkite_314000_Act11(ai, goal, paramTbl)
    local animationId = 3010
    local successDistance = 999 - ai:GetMapHitRadius(TARGET_SELF)
    local angleUp = 0
    local angleDown = 0
    goal:AddSubGoal(GOAL_COMMON_NonspinningAttack, 10, animationId, TARGET_ENE_0, successDistance, angleUp, angleDown)
    return 100
    
end

function Hellkite_314000_Act12(ai, goal, paramTbl)
    local animationId = 3011
    local successDistance = 999 - ai:GetMapHitRadius(TARGET_SELF)
    local angleUp = 0
    local angleDown = 0
    goal:AddSubGoal(GOAL_COMMON_NonspinningAttack, 10, animationId, TARGET_ENE_0, successDistance, angleUp, angleDown)
    return 100
    
end

function Hellkite_314000_Act13(ai, goal, paramTbl)
    local animationId = 3000
    local successDistance = 999 - ai:GetMapHitRadius(TARGET_SELF)
    local angleUp = 0
    local angleDown = 0
    goal:AddSubGoal(GOAL_COMMON_NonspinningAttack, 10, animationId, TARGET_ENE_0, successDistance, angleUp, angleDown)
    return 100
    
end

function Hellkite_314000_Act14(ai, goal, paramTbl)
    local animationId = 3012
    local successDistance = 999 - ai:GetMapHitRadius(TARGET_SELF)
    local angleUp = 0
    local angleDown = 0
    goal:AddSubGoal(GOAL_COMMON_NonspinningAttack, 10, animationId, TARGET_ENE_0, successDistance, angleUp, angleDown)
    return 100
    
end

function Hellkite_314000_Act20(ai, goal, paramTbl)
    goal:AddSubGoal(GOAL_COMMON_Turn, 2, TARGET_ENE_0, 0)
    return 100
    
end

function Hellkite_314000_Act21(ai, goal, paramTbl)
    Hellkite_314000_ApproachToEntity(ai, goal, 3204300, -2.2)
    return 0
    
end

function Hellkite_314000_Act22(ai, goal, paramTbl)
    Hellkite_314000_TurnToEntity(ai, goal, 3204302)
    local successDistance = 999 - ai:GetMapHitRadius(TARGET_SELF)
    local angleUp = 0
    local angleDown = 0
    if ai:IsInsideTargetRegion(TARGET_ENE_0, 3204399) then
        goal:AddSubGoal(GOAL_COMMON_NonspinningAttack, 10, 3015, TARGET_ENE_0, successDistance, angleUp, angleDown)
    end
    return 0
    
end

function Hellkite_314000_Act23(ai, goal, paramTbl)
    Hellkite_314000_TurnToEntity(ai, goal, 3204306, 1)
    local animationId = 3000
    local successDistance = 999 - ai:GetMapHitRadius(TARGET_SELF)
    local angleUp = 0
    local angleDown = 0
    goal:AddSubGoal(GOAL_COMMON_NonspinningAttack, 10, animationId, TARGET_NONE, successDistance, angleUp, angleDown)
    return 0
    
end

function Hellkite_314000_Act24(ai, goal, paramTbl)
    Hellkite_314000_ApproachToEntity(ai, goal, 3204340, -2.2)
    return 0
    
end

function Hellkite_314000_Act25(ai, goal, paramTbl)
    Hellkite_314000_TurnToEntity(ai, goal, 3204353)
    local animationId = 3001
    local successDistance = 999 - ai:GetMapHitRadius(TARGET_SELF)
    local angleUp = 0
    local angleDown = 0
    goal:AddSubGoal(GOAL_COMMON_NonspinningAttack, 10, animationId, TARGET_ENE_0, successDistance, angleUp, angleDown)
    return 0
    
end

function Hellkite_314000_Act26(ai, goal, paramTbl)
    Hellkite_314000_TurnToEntity(ai, goal, 3204301)
    local animationId = 3000
    local successDistance = 999 - ai:GetMapHitRadius(TARGET_SELF)
    local angleUp = 0
    local angleDown = 0
    goal:AddSubGoal(GOAL_COMMON_NonspinningAttack, 10, animationId, TARGET_ENE_0, successDistance, angleUp, angleDown)
    return 0
    
end

function Hellkite_314000_Act27(ai, goal, paramTbl)
    Hellkite_314000_TurnToEntity(ai, goal, 3204301)
    local animationId = 3001
    local successDistance = 999 - ai:GetMapHitRadius(TARGET_SELF)
    local angleUp = 0
    local angleDown = 0
    goal:AddSubGoal(GOAL_COMMON_NonspinningAttack, 10, animationId, TARGET_ENE_0, successDistance, angleUp, angleDown)
    return 0
    
end

function Hellkite_314000_Act28(ai, goal, paramTbl)
    Hellkite_314000_TurnToEntity(ai, goal, 3204301)
    Hellkite_314000_Approach(ai, goal, 16)
    local animationId = 3002
    local successDistance = 999 - ai:GetMapHitRadius(TARGET_SELF)
    local angleUp = 0
    local angleDown = 0
    goal:AddSubGoal(GOAL_COMMON_NonspinningAttack, 10, animationId, TARGET_ENE_0, successDistance, angleUp, angleDown)
    return 0
    
end

function Hellkite_314000_Act29(ai, goal, paramTbl)
    Hellkite_314000_TurnToEntity(ai, goal, 3204304)
    local animationId = 3000
    local successDistance = 999 - ai:GetMapHitRadius(TARGET_SELF)
    local angleUp = 0
    local angleDown = 0
    goal:AddSubGoal(GOAL_COMMON_NonspinningAttack, 10, animationId, TARGET_ENE_0, successDistance, angleUp, angleDown)
    return 0
    
end

function Hellkite_314000_Act30(ai, goal, paramTbl)
    Hellkite_314000_TurnToEntity(ai, goal, 3204304)
    local animationId = 3001
    local successDistance = 999 - ai:GetMapHitRadius(TARGET_SELF)
    local angleUp = 0
    local angleDown = 0
    goal:AddSubGoal(GOAL_COMMON_NonspinningAttack, 10, animationId, TARGET_ENE_0, successDistance, angleUp, angleDown)
    return 0
    
end

function Hellkite_314000_Act31(ai, goal, paramTbl)
    Hellkite_314000_TurnToEntity(ai, goal, 3204304)
    Hellkite_314000_Approach(ai, goal, 16)
    local animationId = 3002
    local successDistance = 999 - ai:GetMapHitRadius(TARGET_SELF)
    local angleUp = 0
    local angleDown = 0
    goal:AddSubGoal(GOAL_COMMON_NonspinningAttack, 10, animationId, TARGET_ENE_0, successDistance, angleUp, angleDown)
    return 0
    
end

function Hellkite_314000_Act32(ai, goal, paramTbl)
    Hellkite_314000_TurnToEntity(ai, goal, 3204303)
    local animationId = 3000
    local successDistance = 999 - ai:GetMapHitRadius(TARGET_SELF)
    local angleUp = 0
    local angleDown = 0
    goal:AddSubGoal(GOAL_COMMON_NonspinningAttack, 10, animationId, TARGET_ENE_0, successDistance, angleUp, angleDown)
    return 0
    
end

function Hellkite_314000_Act33(ai, goal, paramTbl)
    Hellkite_314000_TurnToEntity(ai, goal, 3204303)
    local animationId = 3001
    local successDistance = 999 - ai:GetMapHitRadius(TARGET_SELF)
    local angleUp = 0
    local angleDown = 0
    goal:AddSubGoal(GOAL_COMMON_NonspinningAttack, 10, animationId, TARGET_ENE_0, successDistance, angleUp, angleDown)
    return 0
    
end

function Hellkite_314000_Act34(ai, goal, paramTbl)
    Hellkite_314000_TurnToEntity(ai, goal, 3204303)
    Hellkite_314000_Approach(ai, goal, 16)
    local animationId = 3002
    local successDistance = 999 - ai:GetMapHitRadius(TARGET_SELF)
    local angleUp = 0
    local angleDown = 0
    goal:AddSubGoal(GOAL_COMMON_NonspinningAttack, 10, animationId, TARGET_ENE_0, successDistance, angleUp, angleDown)
    return 0
    
end

function Hellkite_314000_Act35(ai, goal, paramTbl)
    Hellkite_314000_TurnToEntity(ai, goal, 3204355)
    local animationId = 3000
    local successDistance = 999 - ai:GetMapHitRadius(TARGET_SELF)
    local angleUp = 0
    local angleDown = 0
    goal:AddSubGoal(GOAL_COMMON_NonspinningAttack, 10, animationId, TARGET_ENE_0, successDistance, angleUp, angleDown)
    return 0
    
end

function Hellkite_314000_Act36(ai, goal, paramTbl)
    Hellkite_314000_TurnToEntity(ai, goal, 3204355)
    local animationId = 3001
    local successDistance = 999 - ai:GetMapHitRadius(TARGET_SELF)
    local angleUp = 0
    local angleDown = 0
    goal:AddSubGoal(GOAL_COMMON_NonspinningAttack, 10, animationId, TARGET_ENE_0, successDistance, angleUp, angleDown)
    return 0
    
end

function Hellkite_314000_Act37(ai, goal, paramTbl)
    Hellkite_314000_Approach(ai, goal)
    return 0
    
end

function Hellkite_314000_Act38(ai, goal, paramTbl)
    Hellkite_314000_TurnToEntity(ai, goal, 3204357)
    local animationId = 3000
    local successDistance = 999 - ai:GetMapHitRadius(TARGET_SELF)
    local angleUp = 0
    local angleDown = 0
    goal:AddSubGoal(GOAL_COMMON_NonspinningAttack, 10, animationId, TARGET_ENE_0, successDistance, angleUp, angleDown)
    return 0
    
end

function Hellkite_314000_Act39(ai, goal, paramTbl)
    Hellkite_314000_TurnToEntity(ai, goal, 3204357)
    local animationId = 3001
    local successDistance = 999 - ai:GetMapHitRadius(TARGET_SELF)
    local angleUp = 0
    local angleDown = 0
    goal:AddSubGoal(GOAL_COMMON_NonspinningAttack, 10, animationId, TARGET_ENE_0, successDistance, angleUp, angleDown)
    return 0
    
end

function Hellkite_314000_Act40(ai, goal, paramTbl)
    Hellkite_314000_TurnToEntity(ai, goal, 3204305)
    local animationId = 3014
    local successDistance = 999 - ai:GetMapHitRadius(TARGET_SELF)
    local angleUp = 0
    local angleDown = 0
    goal:AddSubGoal(GOAL_COMMON_NonspinningAttack, 10, animationId, TARGET_NONE, successDistance, angleUp, angleDown)
    return 0
    
end

function Hellkite_314000_Act41(ai, goal, paramTbl)
    Hellkite_314000_TurnToEntity(ai, goal, 3204303, 3)
    local animationId = 3014
    local successDistance = 999 - ai:GetMapHitRadius(TARGET_SELF)
    local angleUp = 0
    local angleDown = 0
    goal:AddSubGoal(GOAL_COMMON_NonspinningAttack, 10, animationId, TARGET_NONE, successDistance, angleUp, angleDown)
    return 0
    
end

function Hellkite_314000_Act42(ai, goal, paramTbl)
    ai:SetEventMoveTarget(3204302)
    local angleToPointEvent = ai:GetToTargetAngle(POINT_EVENT)
    if math.abs(angleToPointEvent) <= 90 then
        if ai:IsInsideTargetRegion(TARGET_SELF, 3204321) then
            Hellkite_314000_TurnToEntity(ai, goal, 3204300)
        else
            Hellkite_314000_ApproachToEntity(ai, goal, 3204321, -2.2)
        end
    elseif ai:IsInsideTargetRegion(TARGET_SELF, 3204300) then
        Hellkite_314000_TurnToEntity(ai, goal, 3204321)
    else
        Hellkite_314000_ApproachToEntity(ai, goal, 3204300, -2.2)
    end
    return 0
    
end

function Hellkite_314000_Act43(ai, goal, paramTbl)
    Hellkite_314000_TurnToEntity(ai, goal, 3204307)
    local animationId = 3019
    local successDistance = 999 - ai:GetMapHitRadius(TARGET_SELF)
    local angleUp = 0
    local angleDown = 0
    goal:AddSubGoal(GOAL_COMMON_NonspinningAttack, 10, animationId, TARGET_ENE_0, successDistance, angleUp, angleDown)
    goal:AddSubGoal(GOAL_COMMON_Wait, 3, TARGET_NONE, 0, 0, 0)
    return 0
    
end

function Hellkite_314000_Act44(ai, goal, paramTbl)
    Hellkite_314000_TurnToEntity(ai, goal, 3204308)
    local animationId = 3019
    local successDistance = 999 - ai:GetMapHitRadius(TARGET_SELF)
    local angleUp = 0
    local angleDown = 0
    goal:AddSubGoal(GOAL_COMMON_NonspinningAttack, 10, animationId, TARGET_ENE_0, successDistance, angleUp, angleDown)
    goal:AddSubGoal(GOAL_COMMON_Wait, 3, TARGET_NONE, 0, 0, 0)
    return 0
    
end

function Hellkite_314000_Act45(ai, goal, paramTbl)
    local animationId = 3002
    local successDistance = 999 - ai:GetMapHitRadius(TARGET_SELF)
    local angleUp = 0
    local angleDown = 0
    goal:AddSubGoal(GOAL_COMMON_NonspinningAttack, 10, animationId, TARGET_ENE_0, successDistance, angleUp, angleDown)
    return 100
    
end

function Hellkite_314000_Act46(ai, goal, paramTbl)
    Hellkite_314000_TurnToEntity(ai, goal, 3204354)
    Hellkite_314000_ApproachToEntity(ai, goal, 3204340, -2.2)
    return 0
    
end

function Hellkite_314000_Act50(ai, goal, paramTbl)
    local animationId = 3013
    local successDistance = 999
    local turnTime = 0
    local turnFaceAngle = 0
    goal:AddSubGoal(GOAL_COMMON_AttackTunableSpin, 10, animationId, TARGET_ENE_0, successDistance, turnTime, turnFaceAngle, 0, 0)
    return 0
    
end

function Hellkite_314000_ActAfter_AdjustSpace(ai, goal, paramTbl)
    
end

Goal.Update = function (self, ai, goal)
    return Update_Default_NoSubGoal(self, ai, goal)
    
end

Goal.Terminate = function (self, ai, goal)
    
end

Goal.Interrupt = function (self, ai, goal)
    if ai:IsLadderAct(TARGET_SELF) then
        return false
    end
    if ai:IsInterupt(INTERUPT_Damaged) and ai:IsInsideTargetRegion(TARGET_ENE_0, 3204356) then
        goal:ClearSubGoal()
        goal:AddSubGoal(GOAL_COMMON_AttackTunableSpin, 10, 3000, TARGET_ENE_0, 999, 6, 10, 0, 0)
    end
    if ai:IsInterupt(INTERUPT_Damaged) then
        if ai:IsInsideTargetRegion(TARGET_ENE_0, 3204365) or ai:IsInsideTargetRegion(TARGET_ENE_0, 3204366) or ai:IsInsideTargetRegion(TARGET_ENE_0, 3204367) or ai:IsInsideTargetRegion(TARGET_ENE_0, 3204368) or ai:IsInsideTargetRegion(TARGET_ENE_0, 3204369) or ai:IsInsideTargetRegion(TARGET_ENE_0, 3204370) then
            goal:ClearSubGoal()
            goal:AddSubGoal(GOAL_COMMON_AttackTunableSpin, 10, 3013, TARGET_ENE_0, 999, 0, 0, 0, 0)
        end
        return true
    end
    return false
    
end

function Hellkite_314000_GetMovePontToNearPC(f49_arg0)
    local f49_local0 = 1
    local f49_local1 = {}
    for f49_local2 = 1, table.getn(f0_local0), 1 do
        f49_arg0:SetEventMoveTarget(f0_local0[f49_local2])
        f49_local1[f49_local2] = f49_arg0:GetDistAtoB(POINT_EVENT, TARGET_ENE_0)
    end
    local f49_local2 = 999
    local f49_local3 = 0
    for f49_local4 = 1, table.getn(f49_local1), 1 do
        if f49_local1[f49_local4] <= f49_local2 then
            f49_local2 = f49_local1[f49_local4]
            f49_local3 = f0_local0[f49_local4]
        end
    end
    return f49_local3
    


end

function Hellkite_314000_Approach(f50_arg0, goal, f50_arg2)
    local stopDistance = 0
    if f50_arg2 == nil then
        stopDistance = 0
    else
        stopDistance = f50_arg2
    end
    local f50_local1 = Hellkite_314000_GetMovePontToNearPC(f50_arg0)
    f50_arg0:SetEventMoveTarget(f50_local1)
    f50_arg0:DbgSetLastActIdx(f50_local1 - 3204300)
    goal:AddSubGoal(GOAL_COMMON_Turn, 2, POINT_EVENT, 1)
    goal:AddSubGoal(GOAL_COMMON_ApproachTarget, 1, POINT_EVENT, stopDistance, POINT_EVENT, true, -1)
    return
    
end

function Hellkite_314000_ApproachToEntity(f51_arg0, goal, f51_arg2, f51_arg3)
    if f51_arg2 == nil then
        return
    end
    local stopDistance = 0
    if f51_arg3 == nil then
        stopDistance = 0
    else
        stopDistance = f51_arg3
    end
    f51_arg0:SetEventMoveTarget(f51_arg2)
    f51_arg0:DbgSetLastActIdx(f51_arg2 - 3204300)
    goal:AddSubGoal(GOAL_COMMON_ApproachTarget, 1, POINT_EVENT, stopDistance, POINT_EVENT, true, -1)
    return
    
end

function Hellkite_314000_TurnToEntity(f52_arg0, goal, f52_arg2, f52_arg3)
    local withinAngle = 0
    local goalLife = 1
    if f52_arg2 == nil then
        return
    end
    if f52_arg3 == nil then
        withinAngle = 2.5
    else
        withinAngle = f52_arg3
    end
    f52_arg0:SetEventMoveTarget(f52_arg2)
    f52_arg0:DbgSetLastActIdx(f52_arg2 - 3204300)
    if f52_arg0:IsLookToTarget(POINT_EVENT, withinAngle) then
        goal:AddSubGoal(GOAL_COMMON_Wait, goalLife, TARGET_SELF)
    else
        goal:AddSubGoal(GOAL_COMMON_Turn, 5, POINT_EVENT, withinAngle)
    end
    return
    
end

function Hellkite_314000_MovePointToPoint(ai, f53_arg1)
    ai:SetEventMoveTarget(3204302)
    local angleToPointEvent = ai:GetToTargetAngle(POINT_EVENT)
    if math.abs(angleToPointEvent) <= 90 then
        if ai:IsInsideTargetRegion(TARGET_SELF, 3204321) then
            Hellkite_314000_TurnToEntity(ai, f53_arg1, 3204300)
        else
            Hellkite_314000_ApproachToEntity(ai, f53_arg1, 3204321, -2.2)
        end
    elseif ai:IsInsideTargetRegion(TARGET_SELF, 3204300) then
        Hellkite_314000_TurnToEntity(ai, f53_arg1, 3204321)
    else
        Hellkite_314000_ApproachToEntity(ai, f53_arg1, 3204300, -2.2)
    end
    
end


