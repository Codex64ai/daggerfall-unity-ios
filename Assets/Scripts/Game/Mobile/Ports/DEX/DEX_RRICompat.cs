// MOBILE PORT - source: github.com/SquidKamer/DaggerfallBestiaryProject @ fa21b07331e914d9e622aeaef65a4d8ab9496872
// Ported from DEX 1.3.4 (Kab & Kamer, no licence declared - private draft only); every edit // MOBILE:
// File Scripts/Compatibility/DEX_RRICompat.cs, copied for iOS except lines marked MOBILE (the
// Compatibility/ folder is flattened into Ports/DEX/). One edit: OnStart, which upstream reads
// out of RoleplayRealism-Items' ModSettings by GUID. That mod is compiled in here
// (Ports/RoleplayRealismItems), so it has no .dfmod GUID and no ModSettings to look up; the
// module flags come from the port's own properties instead. The template indices 513-526 below
// are checked against what that port registers by MobileSelfTest.TestDEXPort.
//
using DaggerfallConnect.FallExe;
using DaggerfallWorkshop;
using DaggerfallWorkshop.Game;
using DaggerfallWorkshop.Game.Entity;
using DaggerfallWorkshop.Game.Formulas;
using DaggerfallWorkshop.Game.Items;
using DaggerfallWorkshop.Game.Utility;
using DaggerfallWorkshop.Game.Utility.ModSupport;
using System.Collections.Generic;

namespace DaggerfallBestiaryProject
{
    public static class DEX_RRICompat
    {
        static bool newWeapons = false;
        static bool newArmor = false;

        const int archersAxeTemplateIndex = 513;
        const int lightFlailTemplateIndex = 514;
        const int hauberkTemplateIndex = 515;
        const int chausseTemplateIndex = 516;
        const int leftSpaulderTemplateIndex = 517;
        const int rightSpaulderTemplateIndex = 518;
        const int solleretsTemplateIndex = 519;
        const int jerkinTemplateIndex = 520;
        const int cuisseTemplateIndex = 521;
        const int helmetTemplateIndex = 522;
        const int bootsTemplateIndex = 523;
        const int glovesTemplateIndex = 524;
        const int leftVambraceTemplateIndex = 525;
        const int rightVambraceTemplateIndex = 526;

        public static bool hasRRIRealisticEnemyEquipment = false;

        // MOBILE: the template indices above, in the order this file declares them, so
        // MobileSelfTest can compare them with the ones Ports/RoleplayRealismItems actually
        // registers. Nothing else ties the two ports' numbers together: a drift would not fail to
        // compile, it would quietly dress DEX's class enemies in the wrong items.
        public static readonly int[] TemplateIndices =
        {
            archersAxeTemplateIndex, lightFlailTemplateIndex, hauberkTemplateIndex, chausseTemplateIndex,
            leftSpaulderTemplateIndex, rightSpaulderTemplateIndex, solleretsTemplateIndex, jerkinTemplateIndex,
            cuisseTemplateIndex, helmetTemplateIndex, bootsTemplateIndex, glovesTemplateIndex,
            leftVambraceTemplateIndex, rightVambraceTemplateIndex,
        };

        public static void OnStart()
        {
            // MOBILE: upstream looked RoleplayRealism-Items up by the GUID of its .dfmod and read its
            // ModSettings. Here that mod is compiled into the app (Ports/RoleplayRealismItems) and its
            // data bundle carries no GUID of that name, so GetModFromGUID would always return null and
            // this whole compatibility layer would silently never turn on. The three flags come from
            // the port's own properties instead - the same values, read from the same modsettings.json,
            // by the same mod, one step earlier: RoleplayRealismItemsMod.Awake reads them before
            // MobilePortedMods starts DEX.
            if (!RoleplayRealism.RoleplayRealismItemsMod.Started)
                return;

            hasRRIRealisticEnemyEquipment = RoleplayRealism.RoleplayRealismItemsMod.RealisticEnemyEquipment;
            newWeapons = RoleplayRealism.RoleplayRealismItemsMod.NewWeapons;
            newArmor = RoleplayRealism.RoleplayRealismItemsMod.NewArmor;
            UnityEngine.Debug.Log($"[DEX] RoleplayRealism-Items compatibility on: realisticEnemyEquipment={hasRRIRealisticEnemyEquipment}, newWeapons={newWeapons}, newArmor={newArmor}");
        }

        static bool CoinFlip()
        {
            return UnityEngine.Random.Range(0, 2) == 0;
        }

        static int OneOf(int[] array)
        {
            int i = UnityEngine.Random.Range(0, array.Length);
            return array[i];
        }

        static void AddWornItem(DaggerfallEntity entity, DaggerfallUnityItem item)
        {
            entity.Items.AddItem(item);
            if (item.ItemGroup == ItemGroups.Armor || item.ItemGroup == ItemGroups.Weapons ||
                item.ItemGroup == ItemGroups.MensClothing || item.ItemGroup == ItemGroups.WomensClothing)
            {
                item.currentCondition = (int)(UnityEngine.Random.Range(0.3f, 0.75f) * item.maxCondition);
            }
        }

        static void AddAndEquipWornItem(DaggerfallEntity entity, DaggerfallUnityItem item)
        {
            if (item == null)
                return;

            AddWornItem(entity, item);
            entity.ItemEquipTable.EquipItem(item, true, false);
        }

        static bool HasClassTable(int mobileId, MobileTypes classType)
        {
            if (BestiaryMod.Instance.GetCustomProperties(mobileId, out BestiaryMod.CustomEnemyProperties customEnemyProperties))
            {
                if (!string.IsNullOrEmpty(customEnemyProperties.equipmentTable))
                {
                    if (classType == MobileTypes.Knight_CityWatch)
                    {
                        return customEnemyProperties.equipmentTable.Equals("Guard", System.StringComparison.OrdinalIgnoreCase);
                    }

                    return customEnemyProperties.equipmentTable.Equals(classType.ToString(), System.StringComparison.OrdinalIgnoreCase);
                }
            }

            return mobileId == (int)classType;
        }

        static bool IsFighter(int mobileId)
        {
            return HasClassTable(mobileId, MobileTypes.Knight) || HasClassTable(mobileId, MobileTypes.Warrior);
        }

        static DaggerfallUnityItem CreateWeapon(int templateIndex, WeaponMaterialTypes material)
        {
            if (templateIndex == -1)
                return null;

            DaggerfallUnityItem weapon = ItemBuilder.CreateItem(ItemGroups.Weapons, templateIndex);
            ItemBuilder.ApplyWeaponMaterial(weapon, material);
            return weapon;
        }

        static DaggerfallUnityItem CreateArmor(Genders gender, Races race, int templateIndex, ArmorMaterialTypes material)
        {
            if (templateIndex == -1)
                return null;

            DaggerfallUnityItem armor = ItemBuilder.CreateItem(ItemGroups.Armor, templateIndex);
            ItemBuilder.ApplyArmorSettings(armor, gender, race, material);
            return armor;
        }

        static int GetArmorTemplateIndex(DaggerfallConnect.DFCareer career, EquipSlots type)
        {
            if(!newArmor || ((career.ForbiddenArmors & DaggerfallConnect.DFCareer.ArmorFlags.Plate) == 0) || CoinFlip())
            {
                switch(type)
                {
                    case EquipSlots.ChestArmor: return (int)Armor.Cuirass;
                    case EquipSlots.LegsArmor: return (int)Armor.Greaves;
                    case EquipSlots.LeftArm: return (int)Armor.Left_Pauldron;
                    case EquipSlots.RightArm: return (int)Armor.Right_Pauldron;
                    case EquipSlots.Feet: return (int)Armor.Boots;
                    case EquipSlots.Gloves: return (int)Armor.Gauntlets;
                    case EquipSlots.Head: return (int)Armor.Helm;
                }
            }
            else if((career.ForbiddenArmors & DaggerfallConnect.DFCareer.ArmorFlags.Chain) == 0)
            {
                switch (type)
                {
                    case EquipSlots.ChestArmor: return hauberkTemplateIndex;
                    case EquipSlots.LegsArmor: return chausseTemplateIndex;
                    case EquipSlots.LeftArm: return leftSpaulderTemplateIndex;
                    case EquipSlots.RightArm: return rightSpaulderTemplateIndex;
                    case EquipSlots.Feet: return solleretsTemplateIndex;
                    case EquipSlots.Gloves: return glovesTemplateIndex;
                    case EquipSlots.Head: return helmetTemplateIndex;
                }
            }
            else if ((career.ForbiddenArmors & DaggerfallConnect.DFCareer.ArmorFlags.Leather) == 0)
            {
                switch (type)
                {
                    case EquipSlots.ChestArmor: return jerkinTemplateIndex;
                    case EquipSlots.LegsArmor: return cuisseTemplateIndex;
                    case EquipSlots.LeftArm: return leftVambraceTemplateIndex;
                    case EquipSlots.RightArm: return rightVambraceTemplateIndex;
                    case EquipSlots.Feet: return bootsTemplateIndex;
                    case EquipSlots.Gloves: return glovesTemplateIndex;
                    case EquipSlots.Head: return helmetTemplateIndex;
                }
            }

            return -1;
        }

        private static void ConvertOrcish(EnemyEntity enemyEntity)
        {
            // Orcs have any higher materials converted to Orcish 80% of the time.
            if (enemyEntity.MobileEnemy.Team == MobileTeams.Orcs && Dice100.SuccessRoll(80))
            {
                int convertFrom = (int)WeaponMaterialTypes.Ebony;
                if (enemyEntity.MobileEnemy.ID == (int)MobileTypes.OrcWarlord)
                    convertFrom = (int)WeaponMaterialTypes.Mithril;
                List<DaggerfallUnityItem> items = enemyEntity.Items.SearchItems(ItemGroups.Weapons);
                items.AddRange(enemyEntity.Items.SearchItems(ItemGroups.Armor));
                foreach (DaggerfallUnityItem item in items)
                {
                    int material = item.nativeMaterialValue & 0xFF;
                    if (material >= convertFrom)
                    {
                        ItemTemplate template = item.ItemTemplate;
                        item.weightInKg = template.baseWeight;
                        item.value = template.basePrice;
                        item.currentCondition = template.hitPoints;
                        item.maxCondition = template.hitPoints;
                        item.enchantmentPoints = template.enchantmentPoints;

                        if (item.ItemGroup == ItemGroups.Armor)
                        {
                            ItemBuilder.ApplyArmorMaterial(item, ArmorMaterialTypes.Orcish);
                        }
                        else
                        {
                            if (GameManager.Instance.PlayerEntity.Gender == Genders.Female)
                                item.PlayerTextureArchive += 1;
                            ItemBuilder.ApplyWeaponMaterial(item, WeaponMaterialTypes.Orcish);
                        }
                    }
                }
            }
        }

        static Armor RandomShield()
        {
            return (Armor)UnityEngine.Random.Range((int)Armor.Buckler, (int)Armor.Round_Shield + 1);
        }

        static int RandomLongblade()
        {
            return UnityEngine.Random.Range((int)Weapons.Broadsword, (int)Weapons.Longsword + 1);
        }

        static int RandomShortblade()
        {
            if (Dice100.SuccessRoll(40))
                return (int)Weapons.Shortsword;
            else
                return UnityEngine.Random.Range((int)Weapons.Dagger, (int)Weapons.Wakazashi + 1);
        }

        static Weapons RandomBow()
        {
            return (Weapons)UnityEngine.Random.Range((int)Weapons.Short_Bow, (int)Weapons.Long_Bow + 1);
        }

        static int RandomBigWeapon()
        {
            Weapons weapon = (Weapons)UnityEngine.Random.Range((int)Weapons.Claymore, (int)Weapons.War_Axe + 1);
            if (weapon == Weapons.Dai_Katana && Dice100.SuccessRoll(90))
                weapon = Weapons.Claymore;  // Dai-katana's are very rare.
            return (int)weapon;
        }

        static int[] blunt = new int[] { (int)Weapons.Mace, (int)Weapons.Flail, (int)Weapons.Warhammer };
        static int[] bluntWnew = new int[] { (int)Weapons.Mace, (int)Weapons.Flail, (int)Weapons.Warhammer, lightFlailTemplateIndex };
        static int[] axe = new int[] { (int)Weapons.Battle_Axe, (int)Weapons.War_Axe };
        static int[] axeWnew = new int[] { (int)Weapons.Battle_Axe, (int)Weapons.War_Axe, archersAxeTemplateIndex };

        static int RandomBlunt()
        {
            return OneOf(newWeapons ? bluntWnew : blunt);
        }

        static int RandomAxe()
        {
            return OneOf(newWeapons ? axeWnew : axe);
        }

        private static int RandomAxeOrBlade()
        {
            return CoinFlip() ? RandomAxe() : RandomLongblade();
        }

        private static int RandomBluntOrBlade()
        {
            return CoinFlip() ? RandomBlunt() : RandomLongblade();
        }

        static int SecondaryWeapon()
        {
            switch (UnityEngine.Random.Range(0, 4))
            {
                case 0:
                    return (int)Weapons.Dagger;
                case 1:
                    return (int)Weapons.Shortsword;
                case 2:
                    return newWeapons ? archersAxeTemplateIndex : (int)Weapons.Short_Bow;
                default:
                    return (int)Weapons.Short_Bow;
            }
        }

        static int GetCombatClassWeapon(int mobileId)
        {
            if(HasClassTable(mobileId, MobileTypes.Barbarian))
                return RandomBigWeapon();
            if (HasClassTable(mobileId, MobileTypes.Knight))
                return CoinFlip() ? RandomBlunt() : RandomLongblade();
            if (HasClassTable(mobileId, MobileTypes.Knight_CityWatch))
                return RandomAxeOrBlade();
            if (HasClassTable(mobileId, MobileTypes.Monk))
                return RandomBlunt();
            return RandomLongblade();
        }

        public static void AssignEnemyStartingEquipment(PlayerEntity playerEntity, EnemyEntity enemyEntity, int variant)
        {
            // Use default code for non-class enemies.
            if (enemyEntity.EntityType != EntityTypes.EnemyClass)
            {
                DaggerfallUnity.Instance.ItemHelper.AssignEnemyStartingEquipment(playerEntity, enemyEntity, variant);
                ConvertOrcish(enemyEntity);
                return;
            }
            
            // Set item level, city watch never have items above iron or steel
            int itemLevel = (enemyEntity.MobileEnemy.ID == (int)MobileTypes.Knight_CityWatch) ? 1 : enemyEntity.Level;
            Genders playerGender = playerEntity.Gender;
            Races playerRace = playerEntity.Race;
            int chance = 50;
            int armored = 100;

            // Held weapon(s) and shield/secondary:

            // Ranged specialists:
            if (HasClassTable(enemyEntity.MobileEnemy.ID, MobileTypes.Archer) || HasClassTable(enemyEntity.MobileEnemy.ID, MobileTypes.Ranger))
            {
                AddAndEquipWornItem(enemyEntity, ItemBuilder.CreateWeapon(RandomBow(), FormulaHelper.RandomMaterial(itemLevel)));
                AddWornItem(enemyEntity, CreateWeapon((enemyEntity.MobileEnemy.ID == (int)MobileTypes.Ranger) ? RandomLongblade() : RandomShortblade(), FormulaHelper.RandomMaterial(itemLevel)));
                DaggerfallUnityItem arrowPile = ItemBuilder.CreateWeapon(Weapons.Arrow, WeaponMaterialTypes.Iron);
                arrowPile.stackCount = UnityEngine.Random.Range(4, 17);
                enemyEntity.Items.AddItem(arrowPile);
                armored = 60;
            }
            // Combat classes:
            else if (HasClassTable(enemyEntity.MobileEnemy.ID, MobileTypes.Barbarian)
                || HasClassTable(enemyEntity.MobileEnemy.ID, MobileTypes.Knight)
                || HasClassTable(enemyEntity.MobileEnemy.ID, MobileTypes.Knight_CityWatch)
                || HasClassTable(enemyEntity.MobileEnemy.ID, MobileTypes.Monk)
                || HasClassTable(enemyEntity.MobileEnemy.ID, MobileTypes.Spellsword)
                || HasClassTable(enemyEntity.MobileEnemy.ID, MobileTypes.Warrior)
                || HasClassTable(enemyEntity.MobileEnemy.ID, MobileTypes.Rogue)
            )
            {
                if (variant == 0)
                {
                    AddAndEquipWornItem(enemyEntity, CreateWeapon(GetCombatClassWeapon(enemyEntity.MobileEnemy.ID), FormulaHelper.RandomMaterial(itemLevel)));
                    // Left hand shield?
                    if (Dice100.SuccessRoll(chance))
                        AddAndEquipWornItem(enemyEntity, ItemBuilder.CreateArmor(playerGender, playerRace, RandomShield(), FormulaHelper.RandomArmorMaterial(itemLevel)));
                    // left-hand weapon?
                    else if (Dice100.SuccessRoll(chance))
                        AddAndEquipWornItem(enemyEntity, CreateWeapon(SecondaryWeapon(), FormulaHelper.RandomMaterial(itemLevel)));

                    if (!IsFighter(enemyEntity.MobileEnemy.ID))
                        armored = 80;
                }
                else
                {
                    AddAndEquipWornItem(enemyEntity, CreateWeapon(RandomBigWeapon(), FormulaHelper.RandomMaterial(itemLevel)));
                    if (!IsFighter(enemyEntity.MobileEnemy.ID))
                        armored = 90;
                }

                if (HasClassTable(enemyEntity.MobileEnemy.ID, MobileTypes.Barbarian))
                {   // Barbies tend to forgo armor or use leather
                    armored = 30;
                }
            }
            // Mage classes:
            else if (HasClassTable(enemyEntity.MobileEnemy.ID, MobileTypes.Mage)
            || HasClassTable(enemyEntity.MobileEnemy.ID, MobileTypes.Sorcerer)
            || HasClassTable(enemyEntity.MobileEnemy.ID, MobileTypes.Healer)
            )
            {
                AddAndEquipWornItem(enemyEntity, ItemBuilder.CreateWeapon(Weapons.Staff, FormulaHelper.RandomMaterial(itemLevel)));
                if (Dice100.SuccessRoll(chance))
                    AddWornItem(enemyEntity, CreateWeapon(RandomShortblade(), FormulaHelper.RandomMaterial(itemLevel)));
                AddAndEquipWornItem(enemyEntity, (playerGender == Genders.Male) ? ItemBuilder.CreateMensClothing(MensClothing.Plain_robes, playerEntity.Race) : ItemBuilder.CreateWomensClothing(WomensClothing.Plain_robes, playerEntity.Race));
                armored = 35;
            }
            // Stealthy stabby classes:
            else if (HasClassTable(enemyEntity.MobileEnemy.ID, MobileTypes.Assassin))
            {
                AddAndEquipWornItem(enemyEntity, CreateWeapon(RandomAxeOrBlade(), FormulaHelper.RandomMaterial(itemLevel)));
                AddWornItem(enemyEntity, CreateWeapon(RandomAxeOrBlade(), FormulaHelper.RandomMaterial(itemLevel)));
                armored = 65;
            }
            else if (HasClassTable(enemyEntity.MobileEnemy.ID, MobileTypes.Battlemage))
            {
                AddAndEquipWornItem(enemyEntity, CreateWeapon(RandomBluntOrBlade(), FormulaHelper.RandomMaterial(itemLevel)));
                AddWornItem(enemyEntity, CreateWeapon(RandomBluntOrBlade(), FormulaHelper.RandomMaterial(itemLevel)));
                armored = 75;
            }
            // Sneaky classes:
            else if (HasClassTable(enemyEntity.MobileEnemy.ID, MobileTypes.Acrobat)
                || HasClassTable(enemyEntity.MobileEnemy.ID, MobileTypes.Bard)
                || HasClassTable(enemyEntity.MobileEnemy.ID, MobileTypes.Burglar)
                || HasClassTable(enemyEntity.MobileEnemy.ID, MobileTypes.Nightblade)
                || HasClassTable(enemyEntity.MobileEnemy.ID, MobileTypes.Thief)
            )
            {
                AddAndEquipWornItem(enemyEntity, CreateWeapon(RandomShortblade(), FormulaHelper.RandomMaterial(itemLevel)));
                if (Dice100.SuccessRoll(chance))
                    AddAndEquipWornItem(enemyEntity, CreateWeapon(SecondaryWeapon(), FormulaHelper.RandomMaterial(itemLevel / 2)));
                armored = 50;
            }
            // Unknown
            else
            {
                AddAndEquipWornItem(enemyEntity, CreateWeapon(RandomLongblade(), FormulaHelper.RandomMaterial(itemLevel)));
                armored = 50;
            }

            // Torso
            if (Dice100.SuccessRoll(armored))
                AddAndEquipWornItem(enemyEntity, CreateArmor(playerGender, playerRace, GetArmorTemplateIndex(enemyEntity.Career, EquipSlots.ChestArmor), FormulaHelper.RandomArmorMaterial(itemLevel)));
            armored -= 10;
            // Legs (Barbarians have a raised chance)
            if (Dice100.SuccessRoll(armored) || (enemyEntity.MobileEnemy.ID == (int)MobileTypes.Barbarian && Dice100.SuccessRoll(armored + 50)))
                AddAndEquipWornItem(enemyEntity, CreateArmor(playerGender, playerRace, GetArmorTemplateIndex(enemyEntity.Career, EquipSlots.LegsArmor), FormulaHelper.RandomArmorMaterial(itemLevel)));
            armored -= 10;
            // Feet
            if (Dice100.SuccessRoll(armored))
                AddAndEquipWornItem(enemyEntity, CreateArmor(playerGender, playerRace, GetArmorTemplateIndex(enemyEntity.Career, EquipSlots.Feet), FormulaHelper.RandomArmorMaterial(itemLevel)));
            armored -= 10;
            // Head (Barbarians have a raised chance)
            if (Dice100.SuccessRoll(armored) || (enemyEntity.MobileEnemy.ID == (int)MobileTypes.Barbarian && Dice100.SuccessRoll(armored + 50)))
                AddAndEquipWornItem(enemyEntity, CreateArmor(playerGender, playerRace, GetArmorTemplateIndex(enemyEntity.Career, EquipSlots.Head), FormulaHelper.RandomArmorMaterial(itemLevel)));
            armored -= 20;

            if (armored > 0)
            {
                // right arm
                if (Dice100.SuccessRoll(armored))
                    AddAndEquipWornItem(enemyEntity, CreateArmor(playerGender, playerRace, GetArmorTemplateIndex(enemyEntity.Career, EquipSlots.RightArm), FormulaHelper.RandomArmorMaterial(itemLevel)));
                // left arm
                if (Dice100.SuccessRoll(armored))
                    AddAndEquipWornItem(enemyEntity, CreateArmor(playerGender, playerRace, GetArmorTemplateIndex(enemyEntity.Career, EquipSlots.LeftArm), FormulaHelper.RandomArmorMaterial(itemLevel)));
                // hands
                if (Dice100.SuccessRoll(armored))
                    AddAndEquipWornItem(enemyEntity, CreateArmor(playerGender, playerRace, GetArmorTemplateIndex(enemyEntity.Career, EquipSlots.Gloves), FormulaHelper.RandomArmorMaterial(itemLevel)));
            }

            // Chance for poisoned weapon
            if (playerEntity.Level > 1)
            {
                DaggerfallUnityItem mainWeapon = enemyEntity.ItemEquipTable.GetItem(EquipSlots.RightHand);
                if (mainWeapon != null)
                {
                    int chanceToPoison = 5;
                    if (HasClassTable(enemyEntity.MobileEnemy.ID, MobileTypes.Assassin))
                        chanceToPoison = 60;

                    if (Dice100.SuccessRoll(chanceToPoison))
                    {
                        // Apply poison
                        mainWeapon.poisonType = (Poisons)UnityEngine.Random.Range(128, 135 + 1);
                    }
                }
            }
        }
    }
}