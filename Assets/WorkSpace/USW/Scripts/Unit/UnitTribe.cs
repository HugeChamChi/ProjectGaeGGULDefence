// 주의: UnEmployed/Gunner/Ninja/Wizard(구 로스터)는 사용하지 않아 제거함. 기존 값의 int가
// UnitData 에셋에 직렬화되어 있어 남은 값들은 원래 정수(4~8)를 명시적으로 고정한다.
// 신규 값을 추가할 때도 기존 값의 정수를 바꾸지 않도록 항상 명시적으로 지정하세요.
public enum UnitTribe { Warrior = 4, Mage = 5, Archer = 6, Rogue = 7, Support = 8 }
