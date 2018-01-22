﻿using Google.Protobuf;
using POGOProtos.Data;
using POGOProtos.Enums;
using POGOProtos.Inventory;
using POGOProtos.Networking.Requests;
using POGOProtos.Networking.Requests.Messages;
using POGOProtos.Networking.Responses;
using POGOProtos.Settings.Master;
using PokemonGoGUI.Enums;
using PokemonGoGUI.Extensions;
using PokemonGoGUI.GoManager.Models;
using PokemonGoGUI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PokemonGoGUI.GoManager
{
    public partial class Manager
    {
        public async Task<MethodResult> TransferPokemon(IEnumerable<PokemonData> pokemonsToTransfer)
        {
            PokemonData settedbuddy = Pokemon.Where(w => w.Id == PlayerData?.BuddyPokemon?.Id && PlayerData?.BuddyPokemon?.Id > 0).Select(w => w).FirstOrDefault();
            PokemonData currentbuddy = settedbuddy ?? new PokemonData();

            var pokemonToTransfer = pokemonsToTransfer.Where(x => x.Favorite != 1 && !x.IsEgg && string.IsNullOrEmpty(x.DeployedFortId) && x.Id != currentbuddy.Id && x != null);

            if (pokemonsToTransfer.Count() == 0)
                return new MethodResult();

            LogCaller(new LoggerEventArgs(String.Format("Transferring {0} pokemon", pokemonToTransfer.Count()), LoggerTypes.Info));

            if (!UserSettings.TransferAtOnce)
            {
                foreach (PokemonData pokemon in pokemonToTransfer)
                {
                    var message = new ReleasePokemonMessage { PokemonId = pokemon.Id };

                    var response = await _client.ClientSession.RpcClient.SendRemoteProcedureCallAsync(new Request
                    {
                        RequestType = RequestType.ReleasePokemon,
                        RequestMessage = message.ToByteString()
                    });

                    if (response == null)
                        return new MethodResult();

                    ReleasePokemonResponse releasePokemonResponse = ReleasePokemonResponse.Parser.ParseFrom(response);
                    switch (releasePokemonResponse.Result)
                    {
                        case ReleasePokemonResponse.Types.Result.Success:
                            LogCaller(new LoggerEventArgs(String.Format("Successully transferred {0}. Cp: {1}. IV: {2:0.00}%",
                                pokemon.PokemonId,
                                pokemon.Cp,
                                CalculateIVPerfection(pokemon)),
                                LoggerTypes.Transfer));

                            await Task.Delay(CalculateDelay(UserSettings.DelayBetweenPlayerActions, UserSettings.PlayerActionDelayRandom));

                            RemoveInventoryItem(GetPokemonHashKey(pokemon.Id));

                            continue;
                        case ReleasePokemonResponse.Types.Result.ErrorPokemonIsBuddy:
                            LogCaller(new LoggerEventArgs(String.Format("Faill to transfer {0}. Because: {1}.",
                                pokemon.PokemonId,
                                releasePokemonResponse.Result), LoggerTypes.Warning));
                            continue;
                        case ReleasePokemonResponse.Types.Result.ErrorPokemonIsEgg:
                            LogCaller(new LoggerEventArgs(String.Format("Faill to transfer {0}. Because: {1}.",
                                pokemon.PokemonId,
                                releasePokemonResponse.Result), LoggerTypes.Warning));
                            continue;
                        case ReleasePokemonResponse.Types.Result.PokemonDeployed:
                            LogCaller(new LoggerEventArgs(String.Format("Faill to transfer {0}. Because: {1}.",
                                pokemon.PokemonId,
                                releasePokemonResponse.Result), LoggerTypes.Warning));
                            continue;
                        case ReleasePokemonResponse.Types.Result.Failed:
                            LogCaller(new LoggerEventArgs(String.Format("Faill to transfer {0}",
                                pokemon.PokemonId,
                                releasePokemonResponse.Result), LoggerTypes.Warning));
                            continue;
                        case ReleasePokemonResponse.Types.Result.Unset:
                            LogCaller(new LoggerEventArgs(String.Format("Faill to transfer {0}. Because: {1}.",
                                pokemon.PokemonId,
                                releasePokemonResponse.Result), LoggerTypes.Warning));
                            continue;
                    }
                }
                return new MethodResult
                {
                    Success = true
                };
            }
            else
            {
                var PokemonIds = pokemonToTransfer.Where(x => x != null && x.PokemonId != PokemonId.Missingno).Select(x => x.Id);

                var response = await _client.ClientSession.RpcClient.SendRemoteProcedureCallAsync(new Request
                {
                    RequestType = RequestType.ReleasePokemon,
                    RequestMessage = new ReleasePokemonMessage
                    {
                        PokemonIds = { PokemonIds }
                    }.ToByteString()
                });

                if (response == null)
                    return new MethodResult();

                ReleasePokemonResponse releasePokemonResponse = ReleasePokemonResponse.Parser.ParseFrom(response);

                switch (releasePokemonResponse.Result)
                {
                    case ReleasePokemonResponse.Types.Result.Success:
                        LogCaller(new LoggerEventArgs(
                            String.Format("Successully candy awarded {0} of {1} Pokemons.",
                                releasePokemonResponse.CandyAwarded,
                                pokemonToTransfer.Count()),
                            LoggerTypes.Transfer));

                        await Task.Delay(CalculateDelay(UserSettings.DelayBetweenPlayerActions, UserSettings.PlayerActionDelayRandom));

                        foreach (var pokemonId in PokemonIds)
                        {
                            RemoveInventoryItem(GetPokemonHashKey(pokemonId));
                        }

                        break;
                    case ReleasePokemonResponse.Types.Result.ErrorPokemonIsBuddy:
                        LogCaller(new LoggerEventArgs(String.Format("Faill to transfer {0}. Because: {1}.",
                            pokemonToTransfer.Count(),
                            releasePokemonResponse.Result), LoggerTypes.Warning));
                        break;
                    case ReleasePokemonResponse.Types.Result.ErrorPokemonIsEgg:
                        LogCaller(new LoggerEventArgs(String.Format("Faill to transfer {0}. Because: {1}.",
                            pokemonToTransfer.Count(),
                            releasePokemonResponse.Result), LoggerTypes.Warning));
                        break;
                    case ReleasePokemonResponse.Types.Result.PokemonDeployed:
                        LogCaller(new LoggerEventArgs(String.Format("Faill to transfer {0}. Because: {1}.",
                            pokemonToTransfer.Count(),
                            releasePokemonResponse.Result), LoggerTypes.Warning));
                        break;
                    case ReleasePokemonResponse.Types.Result.Failed:
                        LogCaller(new LoggerEventArgs(String.Format("Faill to transfer {0}",
                            pokemonToTransfer.Count(),
                            releasePokemonResponse.Result), LoggerTypes.Warning));
                        break;
                    case ReleasePokemonResponse.Types.Result.Unset:
                        LogCaller(new LoggerEventArgs(String.Format("Faill to transfer {0}. Because: {1}.",
                            pokemonToTransfer.Count(),
                            releasePokemonResponse.Result), LoggerTypes.Warning));
                        break;
                }

                return new MethodResult
                {
                    Success = true
                };
            }
        }

        private async Task<MethodResult> TransferFilteredPokemon()
        {
            MethodResult<List<PokemonData>> transferResult = GetPokemonToTransfer();

            if (!transferResult.Success || transferResult.Data.Count == 0)
            {
                return new MethodResult();
            }

            await TransferPokemon(transferResult.Data);

            return new MethodResult
            {
                Success = true,
                Message = "Success"
            };
        }

        public MethodResult<List<PokemonData>> GetPokemonToTransfer()
        {
            if (!UserSettings.TransferPokemon)
            {
                LogCaller(new LoggerEventArgs("Transferring disabled", LoggerTypes.Debug));

                return new MethodResult<List<PokemonData>>
                {
                    Data = new List<PokemonData>(),
                    Message = "Transferring disabled",
                    Success = true
                };
            }

            if (!Pokemon.Any())
            {
                LogCaller(new LoggerEventArgs("You have no pokemon", LoggerTypes.Info));

                return new MethodResult<List<PokemonData>>
                {
                    Message = "You have no pokemon"
                };
            }

            var pokemonToTransfer = new List<PokemonData>();

            IEnumerable<IGrouping<PokemonId, PokemonData>> groupedPokemon = Pokemon.GroupBy(x => x.PokemonId);

            foreach (IGrouping<PokemonId, PokemonData> group in groupedPokemon)
            {
                TransferSetting settings = UserSettings.TransferSettings.FirstOrDefault(x => x.Id == group.Key);

                if (settings == null)
                {
                    LogCaller(new LoggerEventArgs(String.Format("Failed to find transfer settings for pokemon {0}", group.Key), LoggerTypes.Warning));

                    continue;
                }

                if (!settings.Transfer)
                {
                    continue;
                }

                switch (settings.Type)
                {
                    case TransferType.All:
                        pokemonToTransfer.AddRange(group.ToList());
                        break;
                    case TransferType.BelowCP:
                        pokemonToTransfer.AddRange(GetPokemonBelowCP(group, settings.MinCP));
                        break;
                    case TransferType.BelowIVPercentage:
                        pokemonToTransfer.AddRange(GetPokemonBelowIVPercent(group, settings.IVPercent));
                        break;
                    case TransferType.KeepPossibleEvolves:
                        pokemonToTransfer.AddRange(GetPokemonByPossibleEvolve(group, settings.KeepMax));
                        break;
                    case TransferType.KeepStrongestX:
                        pokemonToTransfer.AddRange(GetPokemonByStrongest(group, settings.KeepMax));
                        break;
                    case TransferType.KeepXHighestIV:
                        pokemonToTransfer.AddRange(GetPokemonByIV(group, settings.KeepMax));
                        break;
                    case TransferType.BelowCPAndIVAmount:
                        pokemonToTransfer.AddRange(GetPokemonBelowCPIVAmount(group, settings.MinCP, settings.IVPercent));
                        break;
                    case TransferType.BelowCPOrIVAmount:
                        pokemonToTransfer.AddRange(GetPokemonBelowIVPercent(group, settings.IVPercent));
                        pokemonToTransfer.AddRange(GetPokemonBelowCP(group, settings.MinCP));
                        pokemonToTransfer = pokemonToTransfer.DistinctBy(x => x.Id).ToList();
                        break;
                    case TransferType.Slashed:
                        pokemonToTransfer.AddRange(group.ToList());
                        pokemonToTransfer = pokemonToTransfer.DistinctBy(x => x.IsBad).ToList();
                        break;
                }
            }

            if (UserSettings.TransferSlashPokemons)
            {
                var slashPokemons = Pokemon.Where(x => x.IsBad);
                foreach (var slashPokemon in slashPokemons)
                {
                    var inlist = pokemonToTransfer.FirstOrDefault(x => x.Id == slashPokemon.Id);
                    if (inlist == null)
                    {
                        pokemonToTransfer.Add(slashPokemon);
                    }
                }
            }

            return new MethodResult<List<PokemonData>>
            {
                Data = pokemonToTransfer,
                Message = String.Format("Found {0} pokemon to transfer", pokemonToTransfer.Count),
                Success = true
            };
        }

        private List<PokemonData> GetPokemonBelowCPIVAmount(IGrouping<PokemonId, PokemonData> pokemon, int minCp, double percent)
        {
            var toTransfer = new List<PokemonData>();

            foreach (PokemonData pData in pokemon)
            {
                double perfectResult = CalculateIVPerfection(pData);

                if (perfectResult >= 0 && perfectResult < percent && pData.Cp < minCp)
                {
                    toTransfer.Add(pData);
                }
            }

            return toTransfer;
        }

        private List<PokemonData> GetPokemonBelowCP(IGrouping<PokemonId, PokemonData> pokemon, int minCp)
        {
            return pokemon.Where(x => x.Cp < minCp).ToList();
        }

        private List<PokemonData> GetPokemonBelowIVPercent(IGrouping<PokemonId, PokemonData> pokemon, double percent)
        {
            var toTransfer = new List<PokemonData>();

            foreach (PokemonData pData in pokemon)
            {
                double perfectResult = CalculateIVPerfection(pData);

                if (perfectResult >= 0 && perfectResult < percent)
                {
                    toTransfer.Add(pData);
                }
            }

            return toTransfer;
        }

        private List<PokemonData> GetPokemonByStrongest(IGrouping<PokemonId, PokemonData> pokemon, int amount)
        {
            return pokemon.OrderByDescending(x => x.Cp).Skip(amount).ToList();
        }

        private List<PokemonData> GetPokemonByIV(IGrouping<PokemonId, PokemonData> pokemon, int amount)
        {
            if (!pokemon.Any())
            {
                return new List<PokemonData>();
            }

            //Test out first one to make sure things are correct
            double perfectResult = CalculateIVPerfection(pokemon.First());

            return pokemon.OrderByDescending(x => CalculateIVPerfection(x)).ThenByDescending(x => x.Cp).Skip(amount).ToList();
        }

        private List<PokemonData> GetPokemonByPossibleEvolve(IGrouping<PokemonId, PokemonData> pokemon, int limit)
        {
            PokemonSettings setting = null;
            if (!PokeSettings.TryGetValue(pokemon.Key, out setting))
            {
                LogCaller(new LoggerEventArgs(String.Format("Failed to find settings for pokemon {0}", pokemon.Key), LoggerTypes.Info));

                return new List<PokemonData>();
            }

            Candy pokemonCandy = PokemonCandy.FirstOrDefault(x => x.FamilyId == setting.FamilyId);

            int candyToEvolve = setting.CandyToEvolve;
            int totalPokemon = pokemon.Count();
            int totalCandy = pokemonCandy.Candy_;

            if (candyToEvolve == 0)
            {
                return new List<PokemonData>();
            }

            int maxPokemon = totalCandy / candyToEvolve;

            if (maxPokemon > limit)
            {
                maxPokemon = limit;
            }

            return pokemon.OrderByDescending(x => x.Cp).Skip(maxPokemon).ToList();
        }

        // NOTE: this is the real IV Percent, using only Individual values.
        public static double CalculateIVPerfection(PokemonData pokemon)
        {
            // NOTE: 45 points = 15 at points + 15 def points + 15 sta points
            //  100/45 simplifying is 20/9
            return ((double)pokemon.IndividualAttack + pokemon.IndividualDefense + pokemon.IndividualStamina) * 20 / 9;
        }

        // This other Percent gives different IV % for the same IVs depending of the pokemon level.
        public MethodResult<double> CalculateIVPerfectionUsingMaxCP(PokemonData pokemon)
        {
            MethodResult<PokemonSettings> settingResult = GetPokemonSetting(pokemon.PokemonId);

            if (!settingResult.Success)
            {
                return new MethodResult<double>
                {
                    Data = 0,
                    Message = settingResult.Message
                };
            }

            /*
            if (Math.Abs(pokemon.CpMultiplier + pokemon.AdditionalCpMultiplier) <= 0)
            {
                double perfection = (double)(pokemon.IndividualAttack * 2 + pokemon.IndividualDefense + pokemon.IndividualStamina) / (4.0 * 15.0) * 100.0;

                return new MethodResult<double>
                {
                    Data = perfection,
                    Message = "Success",
                    Success = true
                };
            }*/

            double maxCp = CalculateMaxCpMultiplier(pokemon);
            double minCp = CalculateMinCpMultiplier(pokemon);
            double curCp = CalculateCpMultiplier(pokemon);

            double perfectPercent = (curCp - minCp) / (maxCp - minCp) * 100.0;

            return new MethodResult<double>
            {
                Data = perfectPercent,
                Message = "Success",
                Success = true
            };
        }

        public async Task<MethodResult> FavoritePokemon(IEnumerable<PokemonData> pokemonToFavorite, bool favorite = true)
        {
            foreach (PokemonData pokemon in pokemonToFavorite)
            {
                bool isFavorited = true;
                string message = "unfavorited";

                if (pokemon.Favorite == 0)
                {
                    isFavorited = false;
                    message = "favorited";
                }

                if (isFavorited == favorite)
                {
                    continue;
                }


                var response = await _client.ClientSession.RpcClient.SendRemoteProcedureCallAsync(new Request
                {
                    RequestType = RequestType.SetFavoritePokemon,
                    RequestMessage = new SetFavoritePokemonMessage
                    {
                        PokemonId = (long)pokemon.Id,
                        IsFavorite = favorite
                    }.ToByteString()
                });

                if (response == null)
                    return new MethodResult();

                SetFavoritePokemonResponse setFavoritePokemonResponse = null;

                setFavoritePokemonResponse = SetFavoritePokemonResponse.Parser.ParseFrom(response);
                LogCaller(new LoggerEventArgs(
                    String.Format("Successully {3} {0}. Cp: {1}. IV: {2:0.00}%",
                        pokemon.PokemonId,
                        pokemon.Cp,
                        CalculateIVPerfection(pokemon), message),
                    LoggerTypes.Info));

                UpdateInventory(InventoryRefresh.Pokemon);

                await Task.Delay(CalculateDelay(UserSettings.DelayBetweenPlayerActions, UserSettings.PlayerActionDelayRandom));

                return new MethodResult
                {
                    Success = true
                };
            }
            return new MethodResult();
        }

        private double CalculateMaxCpMultiplier(PokemonData poke)
        {
            PokemonSettings pokemonSettings = GetPokemonSetting(poke.PokemonId).Data;

            return (pokemonSettings.Stats.BaseAttack + 15) * Math.Sqrt(pokemonSettings.Stats.BaseDefense + 15) *
            Math.Sqrt(pokemonSettings.Stats.BaseStamina + 15);
        }

        private double CalculateCpMultiplier(PokemonData poke)
        {
            PokemonSettings pokemonSettings = GetPokemonSetting(poke.PokemonId).Data;

            return (pokemonSettings.Stats.BaseAttack + poke.IndividualAttack) *
            Math.Sqrt(pokemonSettings.Stats.BaseDefense + poke.IndividualDefense) *
            Math.Sqrt(pokemonSettings.Stats.BaseStamina + poke.IndividualStamina);
        }

        private double CalculateMinCpMultiplier(PokemonData poke)
        {
            PokemonSettings pokemonSettings = GetPokemonSetting(poke.PokemonId).Data;

            return pokemonSettings.Stats.BaseAttack * Math.Sqrt(pokemonSettings.Stats.BaseDefense) * Math.Sqrt(pokemonSettings.Stats.BaseStamina);
        }

        public async Task<MethodResult> UpgradePokemon(IEnumerable<PokemonData> pokemonsToUpgrade)
        {
            if (pokemonsToUpgrade.Count() == 0)
                return new MethodResult();

            foreach (var pokemon in pokemonsToUpgrade)
            {
                var response = await _client.ClientSession.RpcClient.SendRemoteProcedureCallAsync(new Request
                {
                    RequestType = RequestType.UpgradePokemon,
                    RequestMessage = new UpgradePokemonMessage
                    {
                        PokemonId = pokemon.Id
                    }.ToByteString()
                });

                if (response == null)
                    return new MethodResult();

                var upgradePokemonResponse = UpgradePokemonResponse.Parser.ParseFrom(response);

                switch (upgradePokemonResponse.Result)
                {
                    case UpgradePokemonResponse.Types.Result.Success:
                        UpdateInventory(InventoryRefresh.Pokemon);
                        LogCaller(new LoggerEventArgs(String.Format("Upgrade pokemon {0} success.", pokemon.PokemonId), LoggerTypes.Success));
                        break;
                     case UpgradePokemonResponse.Types.Result.ErrorInsufficientResources:
                        LogCaller(new LoggerEventArgs(String.Format("Failed to upgrade pokemon. Response: {0}", upgradePokemonResponse.Result), LoggerTypes.Warning));
                        break;
                    case UpgradePokemonResponse.Types.Result.ErrorPokemonIsDeployed:
                        LogCaller(new LoggerEventArgs(String.Format("Failed to upgrade pokemon. Response: {0}", upgradePokemonResponse.Result), LoggerTypes.Warning));
                        break;
                    case UpgradePokemonResponse.Types.Result.ErrorPokemonNotFound:
                        LogCaller(new LoggerEventArgs(String.Format("Failed to upgrade pokemon. Response: {0}", upgradePokemonResponse.Result), LoggerTypes.Warning));
                        break;
                    case UpgradePokemonResponse.Types.Result.ErrorUpgradeNotAvailable:
                        LogCaller(new LoggerEventArgs(String.Format("Failed to upgrade pokemon. Response: {0}", upgradePokemonResponse.Result), LoggerTypes.Warning));
                        break;
                    case UpgradePokemonResponse.Types.Result.Unset:
                        LogCaller(new LoggerEventArgs(String.Format("Failed to upgrade pokemon. Response: {0}", upgradePokemonResponse.Result), LoggerTypes.Warning));
                        break;
                }
            }

            return new MethodResult
            {
                Success = true,
                Message = "Success",
            };
        }

        private float GetLevelFromCpMultiplier(double combinedCpMultiplier)
        {
            double level;
            if (combinedCpMultiplier < 0.734f)
            {
                // compute polynomial approximation obtained by regression
                level = 58.35178527 * combinedCpMultiplier * combinedCpMultiplier
                        - 2.838007664 * combinedCpMultiplier + 0.8539209906;
            }
            else
            {
                // compute linear approximation obtained by regression
                level = 171.0112688 * combinedCpMultiplier - 95.20425243;
            }
            // round to nearest .5 value and return
            return (float)(Math.Round((level) * 2) / 2.0);
        }

        /* Un-Used reference only
        private double GetPokemonLevel(double cpMultiplier)
        {
            double pokemonLevel;
            if (cpMultiplier < 0.734)
            {
                pokemonLevel = (58.35178527 * cpMultiplier * cpMultiplier - 2.838007664 * cpMultiplier + 0.8539209906);
            }
            else
            {
                pokemonLevel = 171.0112688 * cpMultiplier - 95.20425243;
            }
            pokemonLevel = (Math.Round(pokemonLevel) * 2) / 2;

            return pokemonLevel;
        }
        */

        private int GetStardustCostsForPowerup(double combinedCpMultiplier, int level)
        {
            return _client.ClientSession.Templates.ItemTemplates.Select(x => x.PokemonUpgrades.StardustCost[level]).FirstOrDefault();
        }

        private int GetCandyCostsForPowerup(double combinedCpMultiplier, int level)
        {
            return _client.ClientSession.Templates.ItemTemplates.Select(x => x.PokemonUpgrades.CandyCost[level]).FirstOrDefault();
        }

        public bool CanUpgradePokemon(PokemonData pokemon)
        {
            // Can't upgrade pokemon in gyms.
            if (!string.IsNullOrEmpty(pokemon.DeployedFortId))
                return false;

            int pokemonLevel = (int)GetLevelFromCpMultiplier(pokemon.CpMultiplier + pokemon.AdditionalCpMultiplier);

            // Can't evolve unless pokemon level is lower than trainer.
            if (pokemonLevel >=  Level + 2)
                return false;

            int familyCandy = PokemonCandy.Where(x => x.FamilyId == GetPokemonSetting(pokemon.PokemonId).Data.FamilyId).FirstOrDefault().Candy_;

            // Can't evolve if not enough candy.
            int pokemonCandyNeededAlready = GetCandyCostsForPowerup(pokemon.CpMultiplier + pokemon.AdditionalCpMultiplier, pokemonLevel);
            if (familyCandy < pokemonCandyNeededAlready)
                return false;

            // Can't evolve if not enough stardust.
            var stardustToUpgrade = GetStardustCostsForPowerup(pokemon.CpMultiplier + pokemon.AdditionalCpMultiplier, pokemonLevel);
            if (TotalStardust < stardustToUpgrade)
                return false;

            return true;
        }
    }
}
