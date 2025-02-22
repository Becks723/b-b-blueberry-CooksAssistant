﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LoveOfCooking.Objects
{
	public class CookingSkill : SpaceCore.Skills.Skill
	{
		private static ITranslationHelper i18n => ModEntry.Instance.Helper.Translation;
		public static readonly string InternalName = ModEntry.AssetPrefix + "CookingSkill"; // DO NOT EDIT

		public static int MaxFoodStackPerDayForExperienceGains;
		public static int CraftNettleTeaLevel;

		public static int GiftBoostValue;
		public static float SalePriceModifier;
		public static float ExtraPortionChance;
		public static int RestorationValue;
		public static int RestorationAltValue;
		public static int BuffRateValue;
		public static int BuffDurationValue;

		public static float BurnChanceReduction;
		public static float BurnChanceModifier;

		public static readonly IList<string> StartingRecipes = new List<string>();
		public static readonly IDictionary<int, IList<string>> CookingSkillLevelUpRecipes = new Dictionary<int, IList<string>>();
		public static readonly IDictionary<string, int> FoodsThatBuffCookingSkill = new Dictionary<string, int>();

		public class SkillProfession : SpaceCore.Skills.Skill.Profession
		{
			public SkillProfession(SpaceCore.Skills.Skill skill, string theId) : base(skill, theId) {}
	            
			internal string Name { get; set; }
			internal string Description { get; set; }
			public override string GetName() { return Name; }
			public override string GetDescription() { return Description; }
		}

		public CookingSkill() : base(InternalName)
		{
			Log.D($"Registering skill {InternalName}",
				ModEntry.Config.DebugMode);

			// Read class values from definitions data file
			var cookingSkillValues = Game1.content.Load
				<Dictionary<string, string>>
				(AssetManager.GameContentSkillValuesPath);
			System.Reflection.FieldInfo[] fields = this
				.GetType()
				.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
			foreach (System.Reflection.FieldInfo field in fields)
			{
				System.Type type = field.GetValue(this).GetType();
				if (type == typeof(int))
					field.SetValue(this, int.Parse(cookingSkillValues[field.Name]));
				else if (type == typeof(float))
					field.SetValue(this, float.Parse(cookingSkillValues[field.Name]));
				else if (type == typeof(double))
					field.SetValue(this, double.Parse(cookingSkillValues[field.Name]));
			}

			// Read cooking skill level up recipes from data file
			var cookingSkillLevelUpTable = Game1.content.Load
				<Dictionary<string, List<string>>>
				(AssetManager.GameContentSkillRecipeTablePath);
			foreach (KeyValuePair<string, List<string>> pair in cookingSkillLevelUpTable)
			{
				CookingSkillLevelUpRecipes.Add(int.Parse(pair.Key), pair.Value);
			}

			// Read starting recipes from general data file
			foreach (string entry in ModEntry.ItemDefinitions["StartingRecipes"])
			{
				StartingRecipes.Add(entry);
			}

			// Set experience values
			List<int> experienceBarColourSplit = cookingSkillValues["ExperienceBarColor"]
				.Split(' ')
				.ToList()
				.ConvertAll(int.Parse);
			ExperienceBarColor = new Color(experienceBarColourSplit[0], experienceBarColourSplit[1], experienceBarColourSplit[2]);
			ExperienceCurve = new[] { 100, 380, 770, 1300, 2150, 3300, 4800, 6900, 10000, 15000 }; // values same as for base game skills

			int size;

			// Set the skills page icon (cookpot)
			size = 10;
			Texture2D texture = new Texture2D(Game1.graphics.GraphicsDevice, size, size);
			Color[] pixels = new Color[size * size];
			ModEntry.SpriteSheet.GetData(0, new Rectangle(31, 4, size, size), pixels, 0, pixels.Length);
			texture.SetData(pixels);
			SkillsPageIcon = texture;

			// Set the skill level-up icon (pot on table)
			size = 16;
			texture = new Texture2D(Game1.graphics.GraphicsDevice, size, size);
			pixels = new Color[size * size];
			ModEntry.SpriteSheet.GetData(0, new Rectangle(0, 272, size, size), pixels, 0, pixels.Length);
			texture.SetData(pixels);
			Icon = texture;

			// Populate skill professions
			const string professionIdTemplate = "menu.cooking_skill.tier{0}_path{1}{2}";
			Texture2D[] textures = new Texture2D[6];
			for (int i = 0; i < textures.Length; ++i)
			{
				int x = 16 + (i * 16); // <-- Which profession icon to use is decided here
				ModEntry.SpriteSheet.GetData(0, new Rectangle(x, 272, size, size), pixels, 0, pixels.Length); // Pixel data copied from spritesheet
				textures[i] = new Texture2D(Game1.graphics.GraphicsDevice, size, size); // Unique texture created, no shared references
				textures[i].SetData(pixels); // Texture has pixel data applied

				// Set metadata for this profession
				string id = string.Format(professionIdTemplate,
					i < 2 ? 1 : 2, // Tier
					i / 2 == 0 ? i + 1 : i / 2, // Path
					i < 2 ? "" : i % 2 == 0 ? "a" : "b"); // Choice
				string extra = i == 1 && !ModEntry.Config.FoodHealingTakesTime ? "_alt" : "";
				SkillProfession profession = new SkillProfession(this, id)
				{
					Icon = textures[i], // <-- Skill profession icon is applied here
					Name = i18n.Get($"{id}{extra}.name"),
					Description = i18n.Get($"{id}{extra}.description",
					new { // v-- Skill profession description values are tokenised here
						SaleValue = $"{((SalePriceModifier - 1) * 100):0}",
						RestorationAltValue = $"{(RestorationAltValue):0}",
					})
				};
				// Skill professions are paired and applied
				Professions.Add(profession);
				if (i > 0 && i % 2 == 1)
					ProfessionsForLevels.Add(new ProfessionPair(ProfessionsForLevels.Count == 0 ? 5 : 10,
						Professions[i - 1], Professions[i]));
			}
		}

		public override string GetName()
		{
			return i18n.Get("menu.cooking_recipe.buff.12");
		}
		
		public override List<string> GetExtraLevelUpInfo(int level)
		{
			var list = new List<string>();
			if (ModEntry.Config.FoodCanBurn)
			{
				list.Add(i18n.Get("menu.cooking_skill.levelup_burn", new
					{
						Number = $"{(level * BurnChanceModifier * BurnChanceReduction):0.00}"
					}));
			}

			Translation extra = i18n.Get($"menu.cooking_skill.levelupbonus.{level}");
			if (extra.HasValue() && (level != CraftNettleTeaLevel || Utils.AreNettlesActive()))
			{
				list.Add(extra);
			}

			return list;
		}

		public override string GetSkillPageHoverText(int level)
		{
			string hoverText = string.Empty;

			if (ModEntry.Config.FoodCanBurn)
			{
				hoverText += Environment.NewLine + i18n.Get(
					key: "menu.cooking_skill.levelup_burn",
					tokens: new
					{
						Number = $"{(level * BurnChanceModifier * BurnChanceReduction):0.00}"
					});
			}

			return hoverText;
		}
	}
}
