// 주의: Wizard는 기존 "고블린 마법사"(FrogWizard, 식량 감소 디버프) 전용 값이라
// "용사 파티" 마법사(FrogMage)와는 다른 직업군입니다. 신규 값은 항상 맨 뒤에 추가하세요
// (기존 값의 int가 UnitData 에셋에 직렬화되어 있어 순서를 바꾸면 안 됩니다).
//
// 주의: Gunner/Ninja도 각각 기존 유닛(Frog_Gunner, Frog_Ninja - 총/쿠나이 컨셉)이 이미
// 사용 중인 값이라 "용사 파티" 궁수(FrogArcher, 활 컨셉)와는 다른 직업군입니다.
//
// 주의: "용사 파티" 도적(FrogRogue, 단검/쿠나이/표창 컨셉)도 겉보기엔 Ninja(Frog_Ninja,
// 쿠나이 컨셉)와 무기 모티브가 겹치지만, Ninja는 이미 "레어 닌굴이" 계열의 별개 캐릭터
// (characterId 1008~1011, IngameUnitData/Ninja_*.asset, skillName "대형 수리검" 등)가
// 사용 중인 값이라 재사용하지 않고 Rogue를 신규로 추가해 사용합니다.
//
// 주의: "용사 파티" 사제(FrogPriest, 백색 의복/십자가 컨셉)의 직업 분류는 [지원가]라
// 기존 8개 값(UnEmployed/Gunner/Ninja/Wizard/Warrior/Mage/Archer/Rogue) 중 어느 것도
// 컨셉이 겹치지 않습니다 — 신규 값 Support를 맨 뒤에 추가해 사용합니다.
public enum UnitTribe { UnEmployed, Gunner, Ninja, Wizard, Warrior, Mage, Archer, Rogue, Support }
