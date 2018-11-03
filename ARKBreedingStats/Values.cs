﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Windows.Forms;
using ARKBreedingStats.species;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ARKBreedingStats
{
    [DataContract]
    public class Values
    {
        private static Values _V;
        [DataMember]
        private string ver = "0.0";
        public Version version = new Version(0, 0);
        public Version modVersion = new Version(0, 0);
        public string modValuesFile = "";
        [DataMember]
        public List<Species> species = new List<Species>();

        public List<string> speciesNames = new List<string>();
        private Dictionary<string, string> aliases;
        public List<string> speciesWithAliasesList;
        private Dictionary<string, string> speciesBlueprints;

        [DataMember]
        public double[][] statMultipliers = new double[8][]; // official server stats-multipliers
        [DataMember]
        public double?[][] statMultipliersSP = new double?[8][]; // adjustments for sp
        [DataMember]
        public Dictionary<string, TamingFood> foodData = new Dictionary<string, TamingFood>();

        public double imprintingStatScaleMultiplier = 1;
        public double babyFoodConsumptionSpeedMultiplier = 1;
        public double babyCuddleIntervalMultiplier = 1;
        public double tamingSpeedMultiplier = 1;

        [DataMember]
        public double matingIntervalMultiplierSP = 1;
        [DataMember]
        public double eggHatchSpeedMultiplierSP = 1;
        [DataMember]
        public double babyMatureSpeedMultiplierSP = 1;
        [DataMember]
        public double babyCuddleIntervalMultiplierSP = 1;
        [DataMember]
        public double tamingSpeedMultiplierSP = 1;
        public bool celsius = true;

        public List<string> glowSpecies = new List<string>(); // this List is used to determine if different stat-names should be displayed

        public Values() { }

        public static Values V => _V ?? (_V = new Values());

        public bool loadValues()
        {
            bool loadedSuccessful = true;

            const string filename = "json/values.json";

            // check if file exists
            if (!File.Exists(filename))
            {
                if (MessageBox.Show("Values-File '" + filename + "' not found. This tool will not work properly without that file.\n\nDo you want to visit the homepage of the tool to redownload it?", "Error", MessageBoxButtons.YesNo, MessageBoxIcon.Error) == DialogResult.Yes)
                    System.Diagnostics.Process.Start("https://github.com/cadon/ARKStatsExtractor/releases/latest");
                return false;
            }

            _V.version = new Version(0, 0);

            DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(Values));
            FileStream file = File.OpenRead(filename);

            try
            {
                _V = (Values)ser.ReadObject(file);
            }
            catch (Exception e)
            {
                MessageBox.Show("File Couldn't be opened or read.\nErrormessage:\n\n" + e.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                loadedSuccessful = false;
            }
            file.Close();

            if (loadedSuccessful)
            {
                try
                {
                    _V.version = new Version(_V.ver);
                }
                catch
                {
                    _V.version = new Version(0, 0);
                }

                _V.speciesNames = new List<string>();
                foreach (Species sp in _V.species)
                {
                    sp.initialize();
                    _V.speciesNames.Add(sp.name);
                }

                OrderSpecies(_V.species, _V.speciesNames);

                _V.glowSpecies = new List<string> { "Bulbdog", "Featherlight", "Glowbug", "Glowtail", "Shinehorn" };
                _V.loadAliases();
                _V.updateSpeciesBlueprints();
                _V.modValuesFile = "";
            }

            //saveJSON();
            return loadedSuccessful;
        }

        public bool loadAdditionalValues(string filename, bool showResults)
        {
            // load extra values-file that can add values or modify existing ones
            bool loadedSuccessful = true;

            // check if file exists
            if (!File.Exists(filename))
            {
                MessageBox.Show("Additional Values-File '" + filename + "' not found.\nThis collection seems to have modified or added values that are saved in a separate file, which couldn't be found at the saved location. You can load it manually via the menu File - Load additional values…", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(Values));
            FileStream file = File.OpenRead(filename);

            Values modifiedValues = new Values();

            try
            {
                modifiedValues = (Values)ser.ReadObject(file);
            }
            catch (Exception e)
            {
                MessageBox.Show("File Couldn't be opened or read.\nErrormessage:\n\n" + e.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                loadedSuccessful = false;
            }
            file.Close();
            if (!loadedSuccessful) return false;

            _V.modValuesFile = Path.GetFileName(file.Name);
            int speciesUpdated = 0;
            int speciesAdded = 0;
            // update data if existing
            // version
            try
            {
                _V.modVersion = new Version(modifiedValues.ver);
            }
            catch
            {
                _V.modVersion = new Version(0, 0);
            }

            // species
            if (modifiedValues.species != null)
            {
                foreach (Species sp in modifiedValues.species)
                {
                    if (!_V.speciesNames.Contains(sp.name))
                    {
                        _V.species.Add(sp);
                        sp.initialize();
                        _V.speciesNames.Add(sp.name);
                        speciesAdded++;
                    }
                    else
                    {
                        // species already exists, update all values which are not null
                        Species originalSpecies = _V.species[_V.speciesNames.IndexOf(sp.name)];
                        bool updated = false;
                        if (sp.TamedBaseHealthMultiplier != null)
                        {
                            originalSpecies.TamedBaseHealthMultiplier = sp.TamedBaseHealthMultiplier;
                            updated = true;
                        }
                        if (sp.NoImprintingForSpeed != null)
                        {
                            originalSpecies.NoImprintingForSpeed = sp.NoImprintingForSpeed;
                            updated = true;
                        }
                        if (sp.statsRaw != null && sp.statsRaw.Length > 0)
                        {
                            for (int s = 0; s < 8 && s < sp.statsRaw.Length; s++)
                            {
                                if (sp.statsRaw[s] == null)
                                    continue;
                                for (int si = 0; si < 5 && si < sp.statsRaw[s].Length; si++)
                                {
                                    if (sp.statsRaw[s][si] == null)
                                        continue;
                                    originalSpecies.statsRaw[s][si] = sp.statsRaw[s][si];
                                    updated = true;
                                }
                            }
                        }
                        if (!string.IsNullOrEmpty(sp.blueprintPath))
                        {
                            originalSpecies.blueprintPath = sp.blueprintPath;
                            updated = true;
                        }
                        if (updated) speciesUpdated++;
                    }
                }

                // sort new species
                OrderSpecies(_V.species, _V.speciesNames);
            }
            // fooddata TODO
            // default-multiplier TODO

            _V.loadAliases();
            _V.updateSpeciesBlueprints();

            if (showResults)
                MessageBox.Show("Species with changed stats: " + speciesUpdated + "\nSpecies added: " + speciesAdded, "Additional Values succesfully added", MessageBoxButtons.OK, MessageBoxIcon.Information);

            return true;
        }

        private void OrderSpecies(List<Species> species, List<string> speciesNames)
        {
            var sortNames = new Dictionary<string, string>();
            const string fileName = "json/sortNames.txt";
            if (File.Exists(fileName))
            {
                foreach (var s in species) s.SortName = "";

                var lines = File.ReadAllLines(fileName);
                foreach (string l in lines)
                {
                    if (l.IndexOf("@") > 0 && l.IndexOf("@") + 1 < l.Length)
                    {
                        string matchName = l.Substring(0, l.IndexOf("@")).Trim();
                        string replaceName = l.Substring(l.IndexOf("@") + 1).Trim();

                        Regex r = new Regex(matchName);

                        var matchedSpecies = species.Where(s => string.IsNullOrEmpty(s.SortName) && r.IsMatch(s.name)).ToList();

                        foreach (var s in matchedSpecies)
                        {
                            s.SortName = r.Replace(s.name, replaceName);
                        }
                    }
                }

                // set each sortname of species without manual sortname to its speciesname
                foreach (var s in species)
                {
                    if (string.IsNullOrEmpty(s.SortName))
                        s.SortName = s.name;
                }
            }

            _V.species = species.OrderBy(s => s.SortName).ToList();
            _V.speciesNames = _V.species.Select(s => s.name).ToList();
        }

        // currently not used
        //public void saveJSON()
        //{
        //    // to create minified json of current values
        //    DataContractJsonSerializer writer = new DataContractJsonSerializer(typeof(Values));
        //    try
        //    {
        //        System.IO.FileStream file = System.IO.File.Create("values.json");
        //        writer.WriteObject(file, _V);
        //        file.Close();
        //    }
        //    catch (Exception e)
        //    {
        //        MessageBox.Show("Error during serialization.\nErrormessage:\n\n" + e.Message, "Serialization-Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //    }
        //}

        public void applyMultipliers(CreatureCollection cc, bool eventMultipliers = false, bool applyStatMultipliers = true)
        {
            imprintingStatScaleMultiplier = cc.imprintingMultiplier;
            babyFoodConsumptionSpeedMultiplier = eventMultipliers ? cc.BabyFoodConsumptionSpeedMultiplierEvent : cc.BabyFoodConsumptionSpeedMultiplier;

            double eggHatchSpeedMultiplier = eventMultipliers ? cc.EggHatchSpeedMultiplierEvent : cc.EggHatchSpeedMultiplier;
            double babyMatureSpeedMultiplier = eventMultipliers ? cc.BabyMatureSpeedMultiplierEvent : cc.BabyMatureSpeedMultiplier;
            double matingIntervalMultiplier = eventMultipliers ? cc.MatingIntervalMultiplierEvent : cc.MatingIntervalMultiplier;
            babyCuddleIntervalMultiplier = eventMultipliers ? cc.babyCuddleIntervalMultiplierEvent : cc.babyCuddleIntervalMultiplier;
            tamingSpeedMultiplier = eventMultipliers ? cc.tamingSpeedMultiplierEvent : cc.tamingSpeedMultiplier;

            if (cc.singlePlayerSettings)
            {
                matingIntervalMultiplier *= matingIntervalMultiplierSP;
                eggHatchSpeedMultiplier *= eggHatchSpeedMultiplierSP;
                babyMatureSpeedMultiplier *= babyMatureSpeedMultiplierSP;
                babyCuddleIntervalMultiplier *= babyCuddleIntervalMultiplierSP;
                tamingSpeedMultiplier *= tamingSpeedMultiplierSP;
            }

            // check for 0
            if (matingIntervalMultiplier == 0) matingIntervalMultiplier = 1;
            if (eggHatchSpeedMultiplier == 0) eggHatchSpeedMultiplier = 1;
            if (babyMatureSpeedMultiplier == 0) babyMatureSpeedMultiplier = 1;
            if (babyCuddleIntervalMultiplier == 0) babyCuddleIntervalMultiplier = 1;
            if (tamingSpeedMultiplier == 0) tamingSpeedMultiplier = 1;

            foreach (Species sp in species)
            {
                if (applyStatMultipliers)
                {
                    // stat-multiplier
                    for (int s = 0; s < 8; s++)
                    {
                        sp.stats[s].BaseValue = (float)sp.statsRaw[s][0];
                        // don't apply the multiplier if AddWhenTamed is negative (e.g. Giganotosaurus, Griffin)
                        sp.stats[s].AddWhenTamed = (float)sp.statsRaw[s][3] * (sp.statsRaw[s][3] > 0 ? (float)cc.multipliers[s][0] : 1);
                        // don't apply the multiplier if MultAffinity is negative (e.g. Aberration variants)
                        sp.stats[s].MultAffinity = (float)sp.statsRaw[s][4] * (sp.statsRaw[s][4] > 0 ? (float)cc.multipliers[s][1] : 1);
                        sp.stats[s].IncPerTamedLevel = (float)sp.statsRaw[s][2] * (float)cc.multipliers[s][2];
                        sp.stats[s].IncPerWildLevel = (float)sp.statsRaw[s][1] * (float)cc.multipliers[s][3];

                        if (!cc.singlePlayerSettings || statMultipliersSP[s] == null)
                            continue;
                        // don't apply the multiplier if AddWhenTamed is negative (e.g. Giganotosaurus, Griffin)
                        sp.stats[s].AddWhenTamed *= statMultipliersSP[s][0] != null && sp.stats[s].AddWhenTamed > 0 ? (float)statMultipliersSP[s][0] : 1;
                        // don't apply the multiplier if MultAffinity is negative (e.g. Aberration variants)
                        sp.stats[s].MultAffinity *= statMultipliersSP[s][1] != null && sp.stats[s].MultAffinity > 0 ? (float)statMultipliersSP[s][1] : 1;
                        sp.stats[s].IncPerTamedLevel *= statMultipliersSP[s][2] != null ? (float)statMultipliersSP[s][2] : 1;
                        sp.stats[s].IncPerWildLevel *= statMultipliersSP[s][3] != null ? (float)statMultipliersSP[s][3] : 1;
                    }
                }
                // breeding multiplier
                if (sp.breeding == null)
                    continue;
                if (eggHatchSpeedMultiplier > 0)
                {
                    sp.breeding.gestationTimeAdjusted = sp.breeding.gestationTime / eggHatchSpeedMultiplier;
                    sp.breeding.incubationTimeAdjusted = sp.breeding.incubationTime / eggHatchSpeedMultiplier;
                }
                if (babyMatureSpeedMultiplier > 0)
                    sp.breeding.maturationTimeAdjusted = sp.breeding.maturationTime / babyMatureSpeedMultiplier;

                sp.breeding.matingCooldownMinAdjusted = sp.breeding.matingCooldownMin * matingIntervalMultiplier;
                sp.breeding.matingCooldownMaxAdjusted = sp.breeding.matingCooldownMax * matingIntervalMultiplier;
            }
        }

        public double[][] getOfficialMultipliers()
        {
            double[][] officialMultipliers = new double[8][];
            for (int s = 0; s < 8; s++)
            {
                officialMultipliers[s] = new double[4];
                for (int sm = 0; sm < 4; sm++)
                    officialMultipliers[s][sm] = statMultipliers[s][sm];
            }
            return officialMultipliers;
        }

        private void loadAliases()
        {
            aliases = new Dictionary<string, string>();
            speciesWithAliasesList = new List<string>(speciesNames);

            const string fileName = "json/aliases.json";
            try
            {
                using (StreamReader reader = File.OpenText(fileName))
                {
                    JObject aliasesNode = (JObject)JToken.ReadFrom(new JsonTextReader(reader));
                    foreach (KeyValuePair<string, JToken> pair in aliasesNode)
                    {
                        if (speciesNames.Contains(pair.Key)
                                || !speciesNames.Contains(pair.Value.Value<string>())
                                || aliases.ContainsKey(pair.Key))
                            continue;
                        aliases.Add(pair.Key, pair.Value.Value<string>());
                        speciesWithAliasesList.Add(pair.Key);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            speciesWithAliasesList.Sort();
        }

        private void updateSpeciesBlueprints()
        {
            speciesBlueprints = new Dictionary<string, string>();

            foreach (Species s in species)
            {
                if (!string.IsNullOrEmpty(s.blueprintPath) && !speciesBlueprints.ContainsKey(s.blueprintPath))
                {
                    speciesBlueprints.Add(s.blueprintPath, s.name);
                }
            }
        }

        public string speciesName(string alias)
        {
            if (speciesNames.Contains(alias))
                return alias;
            return aliases.ContainsKey(alias) ? aliases[alias] : "";
        }

        public string speciesNameFromBP(string blueprintpath)
        {
            return speciesBlueprints.ContainsKey(blueprintpath) ? speciesBlueprints[blueprintpath] : "";
        }

        public int speciesIndex(string species)
        {
            species = speciesName(species);
            return speciesNames.IndexOf(species);
        }
    }
}
