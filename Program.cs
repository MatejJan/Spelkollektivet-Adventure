using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace SpelkollektivetAdventure
{
    #region Data types

    // Enumerations

    enum LocationId
    {
        Nowhere,
        Inventory,
        Entrance,
        Lobby,
        Reception,
        RoundRoom,
        DiningRoom,
        NorthWingEntrance,
        NorthWing,
        YourRoom,
        NorthWingBathroom,
        SouthWingEntrance,
        SouthWing,
        BasementLobby,
        LoudOffice,
        Scullery,
        HomiesKitchen
    }

    enum ThingId
    {
        None,
        All,
        Suitcase,
        James,
        CleanPlate,
        DirtyPlate,
        RinsedPlate,
        Computer,
        Checklist,
        EmptyDesk,
        YourDesk,
        BathroomSign,
        Mop,
        Hair,
        Puddle,
        Shower,
        TrashBin,
        Meatballs
    }

    enum Direction
    {
        North,
        NorthEast,
        East,
        SouthEast,
        South,
        SouthWest,
        West,
        NorthWest,
        Down,
        Up
    }

    enum Goal
    {
        SuitcaseInRoom,
        ComputerInOffice,
        DinnerEaten,
        ShowerTaken
    }

    // Data classes

    class LocationData
    {
        public LocationId Id;
        public string Name;
        public string Description;
        public Dictionary<Direction, LocationId> Directions;
    }

    class ThingData
    {
        public ThingId Id;
        public string Name;
        public string Description;
        public LocationId StartingLocationId;
    }

    class ParsedData
    {
        public string Id;
        public string Name;
        public string Description;
        public Dictionary<Direction, LocationId> Directions;
        public LocationId StartingLocationId;
    }

    #endregion

    class Program
    {
        #region Fields

        // Data dictionaries

        static Dictionary<LocationId, LocationData> LocationsData = new Dictionary<LocationId, LocationData>();
        static Dictionary<ThingId, ThingData> ThingsData = new Dictionary<ThingId, ThingData>();

        // Thing helpers

        static Dictionary<string, ThingId> ThingIdsByName = new Dictionary<string, ThingId>();
        static ThingId[] ThingsYouCanTalkTo = { ThingId.James };
        static ThingId[] ThingsYouCanGet = { ThingId.Suitcase, ThingId.CleanPlate, ThingId.DirtyPlate, ThingId.RinsedPlate, ThingId.Computer, ThingId.Mop, ThingId.Hair, ThingId.Meatballs };
        static ThingId[] ThingsYouCanRead = { ThingId.Checklist, ThingId.BathroomSign };
        static ThingId[] Plates = { ThingId.DirtyPlate, ThingId.CleanPlate, ThingId.RinsedPlate };

        // Current state

        static LocationId CurrentLocationId = LocationId.Entrance;
        static Dictionary<ThingId, LocationId> ThingsLocations = new Dictionary<ThingId, LocationId>();
        static Dictionary<LocationId, bool> LocationSeen = new Dictionary<LocationId, bool>();
        static Dictionary<Goal, bool> GoalCompleted = new Dictionary<Goal, bool>();
        static bool SleepHintGiven = false;

        // Helper variables and constants

        static bool ShouldQuit = false;

        const ConsoleColor NarrativeColor = ConsoleColor.Gray;
        const ConsoleColor PromptColor = ConsoleColor.White;
        const ConsoleColor SuccessColor = ConsoleColor.DarkGreen;
        const int PrintPauseMilliseconds = 50;

        #endregion

        #region Program Start

        static void Main(string[] args)
        {
            // Initialize everything.
            ReadLocationsData();
            ReadThingsData();
            InitializeThingHelpers();
            InitializeThingsState();
            InitializeSeenLocations();
            InitializeGoals();

            // Display intro.
            Console.ForegroundColor = NarrativeColor;

            Print("Welcome to Spelkollektivet!");
            Print();
            Print("You are the newest homie in the house full of indie game developers. Your goal is to settle yourself in and get acquinted to the life in the house. Type 'checklist' to see the list of things you have to do by the end of the day. Your actions and conduct will be evaulated at the end.");
            Print();
            Print("Press any key to begin.");

            Console.ReadKey();

            DisplayLocation(false);

            // Start the main interaction loop.
            while (!ShouldQuit)
            {
                HandlePlayerAction();
                ApplyGameRules();
            }
        }

        // Initialization methods

        static void ReadLocationsData()
        {
            // Parse the locations file.
            List<ParsedData> parsedDataList = ParseData("Locations.txt");

            // Transfer data from the parsed structures into locations data.
            foreach (ParsedData parsedData in parsedDataList)
            {
                LocationId locationId = Enum.Parse<LocationId>(parsedData.Id);
                LocationData locationData = new LocationData
                {
                    Id = locationId,
                    Name = parsedData.Name,
                    Description = parsedData.Description,
                    Directions = parsedData.Directions
                };
                LocationsData[locationId] = locationData;
            }
        }

        static void ReadThingsData()
        {
            // Parse the things file.
            List<ParsedData> parsedDataList = ParseData("Things.txt");

            // Transfer data from the parsed structures into things data.
            foreach (ParsedData parsedData in parsedDataList)
            {
                ThingId thingId = Enum.Parse<ThingId>(parsedData.Id);
                ThingData thingData = new ThingData
                {
                    Id = thingId,
                    Name = parsedData.Name,
                    Description = parsedData.Description,
                    StartingLocationId = parsedData.StartingLocationId
                };
                ThingsData[thingId] = thingData;
            }
        }

        static List<ParsedData> ParseData(string filePath)
        {
            var parsedDataList = new List<ParsedData>();

            string[] dataLines = File.ReadAllLines(filePath);
            var currentLineIndex = 0;

            // Parse data until we reach the end.
            while (currentLineIndex < dataLines.Length)
            {
                // First line of data holds the ID string.
                string id = dataLines[currentLineIndex];

                // Initialize the structure that will hold parsed data.
                var parsedData = new ParsedData
                {
                    Id = id,
                    Directions = new Dictionary<Direction, LocationId>()
                };

                // The remaining lines hold various properties in "property: value" or "property:" format.
                currentLineIndex++;

                do
                {
                    // Extract property and potentially value.
                    MatchCollection matches = Regex.Matches(dataLines[currentLineIndex], @"(\w+): *(.*)?");
                    if (matches.Count == 0)
                    {
                        throw new FormatException("Invalid property line: " + dataLines[currentLineIndex]);
                    }

                    string property = matches[0].Groups[1].Value;
                    string value = matches[0].Groups[2]?.Value;

                    // Store value into data structure.
                    switch (property)
                    {
                        case "Name":
                            parsedData.Name = value;
                            break;

                        case "Description":
                            parsedData.Description = value;
                            break;

                        case "Directions":
                            // Directions are listed in separate lines with format "  direction: destination".
                            do
                            {
                                // Continue while the next line is a directions line.
                                MatchCollection directionsLineMatches = Regex.Matches(dataLines[currentLineIndex + 1], @"[ \t]+(\w+): *(.*)");
                                if (directionsLineMatches.Count == 0) break;

                                // Store parsed data into the directions dictionary.
                                Direction direction = Enum.Parse<Direction>(directionsLineMatches[0].Groups[1].Value);
                                LocationId destination = Enum.Parse<LocationId>(directionsLineMatches[0].Groups[2].Value);
                                parsedData.Directions[direction] = destination;

                                currentLineIndex++;

                            } while (currentLineIndex + 1 < dataLines.Length);
                            break;

                        case "StartingLocation":
                            parsedData.StartingLocationId = Enum.Parse<LocationId>(value);
                            break;
                    }

                    currentLineIndex++;

                    // Keep parsing until we reach an empty line, which signifies the end of the current entry.
                } while (currentLineIndex < dataLines.Length && dataLines[currentLineIndex].Length > 0);

                // All data for this entry was parsed. Store it and skip the next empty line.
                parsedDataList.Add(parsedData);
                currentLineIndex++;
            }

            return parsedDataList;
        }

        static void InitializeThingHelpers()
        {
            // Create a map of things by their name.
            foreach (KeyValuePair<ThingId, ThingData> thingEntry in ThingsData)
            {
                string name = thingEntry.Value.Name.ToLowerInvariant();

                // Allow to refer to a thing by any of its words.
                string[] nameParts = name.Split();

                foreach (string namePart in nameParts)
                {
                    // Don't override already assigned words.
                    if (ThingIdsByName.ContainsKey(namePart)) continue;

                    ThingIdsByName[namePart] = thingEntry.Key;
                }
            }
        }

        static void InitializeThingsState()
        {
            // Set all things to their starting locations.
            foreach (KeyValuePair<ThingId, ThingData> thingEntry in ThingsData)
            {
                ThingsLocations[thingEntry.Key] = thingEntry.Value.StartingLocationId;
            }
        }

        static void InitializeSeenLocations()
        {
            // Set all locations as not visited.
            foreach (LocationId locationId in Enum.GetValues(typeof(LocationId)))
            {
                LocationSeen[locationId] = false;
            }
        }

        static void InitializeGoals()
        {
            // Set all goals as not completed.
            foreach (Goal goal in Enum.GetValues(typeof(Goal)))
            {
                GoalCompleted[goal] = false;
            }
        }

        #endregion

        #region Output Helpers

        /// <summary>
        /// Writes an empty line to the output.
        /// </summary>
        static void Print()
        {
            Console.WriteLine();
            Thread.Sleep(PrintPauseMilliseconds);
        }

        /// <summary>
        /// Writes the specified text to the output.
        /// </summary>
        static void Print(string text)
        {
            // Split text into lines that don't exceed the window width.
            int maximumLineLength = Console.WindowWidth - 1;
            MatchCollection lineMatches = Regex.Matches(text, @"(.{1," + maximumLineLength + @"})(?:\s|$)");

            // Output each line with a small delay.
            foreach (Match match in lineMatches)
            {
                Console.WriteLine(match.Groups[0].Value);
                Thread.Sleep(PrintPauseMilliseconds);
            }
        }

        /// <summary>
        /// Responds to player's command with the specified message.
        /// </summary>
        static void Reply(string text)
        {
            Print(text);
            Print();
        }

        /// <summary>
        /// Returns the text with the first letter in uppercase.
        /// </summary>
        static string Capitalize(string text)
        {
            return text.Substring(0, 1).ToUpperInvariant() + text.Substring(1);
        }

        #endregion

        #region Interaction helpers

        /// <summary>
        /// Returns a list of things that are mentioned in the specified words.
        /// </summary>
        static List<ThingId> GetThingIdsFromWords(string[] words)
        {
            List<ThingId> thingIds = new List<ThingId>();

            // For each word, see if it's a name of a thing.
            foreach (string word in words)
            {
                if (ThingIdsByName.ContainsKey(word))
                {
                    thingIds.Add(ThingIdsByName[word]);
                }
            }

            return thingIds;
        }

        /// <summary>
        /// Returns all things that are at the specified location.
        /// </summary>
        static IEnumerable<ThingId> GetThingsAtLocation(LocationId locationId)
        {
            return ThingsLocations.Keys.Where(thingId => ThingAt(thingId, locationId));

            /* Below code kept to show students how much longer an imperative filter code is.

            List<Thing> thingsAtLocation = new List<Thing>();

            foreach (Thing thing in Enum.GetValues(typeof(Thing)))
            {
                if (ThingAt(thing, location))
                {
                    thingsAtLocation.Add(thing);
                }
            }

            return thingsAtLocation;*/
        }

        /// <summary>
        /// Returns the name of the specified thing.
        /// </summary>
        static string GetName(ThingId thingId)
        {
            return ThingsData[thingId].Name;
        }

        /// <summary>
        /// Returns the names of specified things.
        /// </summary>
        static IEnumerable<string> GetNames(IEnumerable<ThingId> thingIds)
        {
            return thingIds.Select(thingId => ThingsData[thingId].Name);

            /* Below code kept to show students how much longer an imperative map code is.
              
            string[] thingNames = new string[things.Count];

            for (var i = 0; i < things.Count; i++)
            {
                thingNames[i] = ThingsData[things[i]].Name;
            }

            return thingNames;
            */
        }

        /// <summary>
        /// Tells if any of the words are "everything" or "all".
        /// </summary>
        static bool WordsIncludeEverythingSynonyms(string[] words)
        {
            string[] everythingSynonyms = { "everything", "all" };

            return everythingSynonyms.Intersect(words).Count() > 0;
        }

        #endregion

        #region Interaction

        static void HandlePlayerAction()
        {
            // Ask the player what they want to do.
            Print("What now?");
            Print();

            Console.ForegroundColor = PromptColor;
            Console.Write("> ");

            string command = Console.ReadLine().ToLowerInvariant();

            Console.ForegroundColor = NarrativeColor;
            Print();

            // Analyze the command by assuming the first word is a verb (or similar instruction).
            string[] words = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (words.Length == 0)
            {
                Reply("Try typing something.");
                return;
            }

            string verb = words[0];

            // Some commands are performed with things so see if any are being mentioned.
            List<ThingId> thingIds = GetThingIdsFromWords(words);

            // Call the appropriate handler for the given verb.
            switch (verb)
            {
                // Directions

                case "north":
                case "n":
                    HandleMovement(Direction.North);
                    break;

                case "northeast":
                case "ne":
                    HandleMovement(Direction.NorthEast);
                    break;

                case "east":
                case "e":
                    HandleMovement(Direction.East);
                    break;

                case "southeast":
                case "se":
                    HandleMovement(Direction.SouthEast);
                    break;

                case "south":
                case "s":
                    HandleMovement(Direction.South);
                    break;

                case "southwest":
                case "sw":
                    HandleMovement(Direction.SouthWest);
                    break;

                case "west":
                case "w":
                    HandleMovement(Direction.West);
                    break;

                case "northwest":
                case "nw":
                    HandleMovement(Direction.NorthWest);
                    break;

                case "down":
                case "d":
                    HandleMovement(Direction.Down);
                    break;

                case "up":
                case "u":
                    HandleMovement(Direction.Up);
                    break;

                // Main commands

                case "l":
                case "look":
                    HandleLook(words, thingIds);
                    break;

                case "read":
                    HandleRead(words, thingIds);
                    break;

                case "talk":
                    HandleTalk(words, thingIds);
                    break;

                case "get":
                case "pick":
                case "take":
                    HandleGet(words, thingIds);
                    break;

                case "drop":
                case "set":
                case "place":
                case "throw":
                    HandleDrop(words, thingIds);
                    break;

                case "i":
                case "inventory":
                    DisplayInventory();
                    break;

                case "open":
                    HandleOpen(words, thingIds);
                    break;

                case "shower":
                    TakeShower();
                    break;

                case "clean":
                    HandleClean(words, thingIds);
                    break;

                case "mop":
                    MopPuddle();
                    break;

                case "eat":
                    HandleEat(words, thingIds);
                    break;

                case "sleep":
                    HandleSleep();
                    break;

                case "rinse":
                    HandleRinse(words, thingIds);
                    break;

                // Special commands

                case "checklist":
                    DisplayChecklist();
                    break;

                case "end":
                case "quit":
                case "exit":
                    Reply("Goodbye!");
                    ShouldQuit = true;
                    break;

                default:
                    Reply("I do not understand you.");
                    break;
            }
        }

        // Verb handlers

        static void HandleMovement(Direction direction)
        {
            // See if the current location has the desired direction.
            LocationData currentLocationData = LocationsData[CurrentLocationId];

            if (!currentLocationData.Directions.ContainsKey(direction))
            {
                Reply("You cannot go there.");
                return;
            }

            // Move the player to that location.
            CurrentLocationId = currentLocationData.Directions[direction];

            // Display the new location's description.
            DisplayLocation(false);
        }

        static void HandleLook(string[] words, List<ThingId> thingIds)
        {
            if (words.Length == 1)
            {
                // This is the simple look command which just outputs the location description again.
                DisplayLocation(true);
                return;
            }

            // We're trying to look at things. See if any were mentioned.
            if (thingIds.Count == 0)
            {
                Reply("I don't see it.");
                return;
            }

            // Display descriptions of all mentioned things.
            foreach (ThingId thingId in thingIds)
            {
                // Make sure the thing is present.
                if (!ThingAvailable(thingId))
                {
                    Reply($"{Capitalize(GetName(thingId))} is not here.");
                    continue;
                }

                DisplayThing(thingId);
            }
        }

        static void HandleRead(string[] words, List<ThingId> thingIds)
        {
            // Handle edge cases.
            if (words.Length == 1)
            {
                Reply("What do you want to read?");
                return;
            }

            // Make sure we understood what to get.
            if (thingIds.Count == 0)
            {
                Reply("I don't know which thing you want to read.");
                return;
            }

            // Display descriptions of all mentioned things.
            foreach (ThingId thingId in thingIds)
            {
                // Make sure the thing can be read.
                if (!ThingsYouCanRead.Contains(thingId))
                {
                    Reply($"{Capitalize(GetName(thingId))} can't be read.");
                    continue;
                }

                DisplayThing(thingId);
            }
        }

        static void HandleTalk(string[] words, List<ThingId> thingIds)
        {
            // Handle edge cases.
            if (words.Length == 1)
            {
                Reply("Talk to who?");
                return;
            }

            if (thingIds.Count == 0)
            {
                Reply("I don't know who you mean.");
                return;
            }

            // Only consider the first thing mentioned.
            ThingId thingId = thingIds[0];

            // Make sure the thing is an NPC that can be talked to.
            if (!ThingsYouCanTalkTo.Contains(thingId))
            {
                Reply("You can't talk to that.");
                return;
            }

            // Make sure the NPC is present.
            if (!ThingIsHere(thingId))
            {
                Reply($"{Capitalize(GetName(thingId))} is not here.");
                return;
            }

            // Everything seems to be OK, proceed to the talk event with the specific NPC.
            switch (thingId)
            {
                case ThingId.James:
                    TalkToJames();
                    break;
            }
        }

        static void HandleGet(string[] words, List<ThingId> thingIds)
        {
            // Handle edge cases.
            if (words.Length == 1)
            {
                Reply("What do you want to get?");
                return;
            }

            // See if we want to get everything.
            if (WordsIncludeEverythingSynonyms(words))
            {
                IEnumerable<ThingId> thingsAtLocation = GetThingsAtLocation(CurrentLocationId);

                // Find the things we can actually get.
                IEnumerable<ThingId> availableThingsToGet = ThingsYouCanGet.Intersect(thingsAtLocation);

                if (availableThingsToGet.Count() == 0)
                {
                    Reply("There is nothing here to be picked up.");
                    return;
                }

                thingIds = new List<ThingId>(availableThingsToGet);
            }

            // Make sure we understood what to get.
            if (thingIds.Count == 0)
            {
                Reply("I don't know which thing you want to get.");
                return;
            }

            // Try to get all mentioned things.
            List<ThingId> thingsPickedUp = new List<ThingId>();

            foreach (ThingId thingId in thingIds)
            {
                string thingName = Capitalize(GetName(thingId));

                // Make sure the thing can be picked up.
                if (!ThingsYouCanGet.Contains(thingId))
                {
                    // Shower is a special case of things you can 'take' (as a manner of speech).
                    if (thingId == ThingId.Shower)
                    {
                        TakeShower();
                        continue;
                    }

                    Reply($"{thingName} can't be picked up.");
                    continue;
                }

                // Check if you already have the thing.
                if (HaveThing(thingId))
                {
                    Reply($"{thingName} is already in your possession.");
                    continue;
                }

                // Make sure the thing is at this location.
                if (!ThingIsHere(thingId))
                {
                    Reply($"{thingName} is not here.");
                    continue;
                }

                // Everything seems to be OK, take the thing.
                bool pickedUp = GetThing(thingId);
                if (pickedUp) thingsPickedUp.Add(thingId);
            }

            // If nothing was picked up, we let the error messages speak for themselves.
            if (thingsPickedUp.Count == 0) return;

            // If everything was picked up, we simply confirm the command.
            if (thingsPickedUp.Count == thingIds.Count)
            {
                Reply("OK.");
                return;
            }

            // It seems that some items weren't picked up, so in addition to the error messages we want to state what did get picked up.
            Reply($"You picked up {string.Join(", ", GetNames(thingsPickedUp))}.");
        }

        static void HandleDrop(string[] words, List<ThingId> thingIds)
        {
            // Handle edge cases.
            if (words.Length == 1)
            {
                Reply("What do you want to drop?");
                return;
            }

            // When trash is mantioned, we assume we're dropping things into it.
            if (thingIds.Contains(ThingId.TrashBin))
            {
                ThrowInTrash(thingIds.Where(thing => thing != ThingId.TrashBin));
                return;
            }

            // See if we want to drop everything.
            if (WordsIncludeEverythingSynonyms(words))
            {
                // See if we have any things to drop.
                IEnumerable<ThingId> thingsInInventory = GetThingsAtLocation(LocationId.Inventory);

                if (thingsInInventory.Count() == 0)
                {
                    Reply("You aren't carrying anything.");
                    return;
                }

                IEnumerable<ThingId> thingsYouCanDrop = thingsInInventory.Intersect(ThingsYouCanGet);

                if (thingsYouCanDrop.Count() == 0)
                {
                    Reply("You don't have anything you could drop.");
                    return;
                }

                thingIds = new List<ThingId>(thingsYouCanDrop);
            }

            // Make sure we understood what to drop.
            if (thingIds.Count == 0)
            {
                Reply("I don't know which thing you want to drop.");
                return;
            }

            // Try to drop all the mentioned things.
            List<ThingId> thingsDropped = new List<ThingId>();

            foreach (ThingId thingId in thingIds)
            {
                // Make sure you have the thing.
                if (!HaveThing(thingId))
                {
                    Reply($"{Capitalize(GetName(thingId))} is not in your inventory.");
                    continue;
                }

                // Everything seems to be OK, drop the item.
                bool dropped = DropThing(thingId);
                if (dropped) thingsDropped.Add(thingId);
            }

            // If nothing was dropped, we let the error messages speak for themselves.
            if (thingsDropped.Count == 0) return;

            // If everything was dropped, we simply confirm the command.
            if (thingsDropped.Count == thingIds.Count)
            {
                Reply("OK.");
                return;
            }

            // It seems some items weren't dropped, so in addition to the error messages we want to state what did get dropped.
            Reply($"You dropped {string.Join(", ", GetNames(thingsDropped))}.");
        }

        static void HandleOpen(string[] words, List<ThingId> thingIds)
        {
            // Handle edge cases.
            if (words.Length == 1)
            {
                Reply("What do you want to open?");
                return;
            }

            if (thingIds.Count == 0)
            {
                Reply("I don't know which thing you want to open.");
                return;
            }

            if (thingIds.Contains(ThingId.Suitcase))
            {
                OpenSuitcase();
            }
        }

        static void HandleClean(string[] words, List<ThingId> thingIds)
        {
            // Handle edge cases.
            if (words.Length == 1)
            {
                Reply("What do you want to clean?");
                return;
            }

            if (thingIds.Count == 0)
            {
                Reply("I don't know which thing you want to clean.");
                return;
            }

            if (thingIds.Contains(ThingId.Puddle))
            {
                MopPuddle();
            }
            else if (thingIds.Contains(ThingId.Hair))
            {
                Reply("You should pick it up and throw it in the trash.");
            }
            else if (thingIds.Intersect(Plates).Count() > 0)
            {
                RinsePlate();
            }
            else
            {
                Reply("You can't clean that.");
            }
        }

        static void HandleEat(string[] words, List<ThingId> thingIds)
        {
            // Handle edge cases.
            if (words.Length == 1)
            {
                Eat();
                return;
            }

            if (thingIds.Count == 0)
            {
                Reply("I don't know which thing you want to eat.");
                return;
            }

            if (thingIds.Contains(ThingId.Meatballs))
            {
                Eat();
            }
            else
            {
                Reply("You can't eat that.");
            }
        }

        static void HandleRinse(string[] words, List<ThingId> thingIds)
        {
            // Handle edge cases.
            if (words.Length == 1)
            {
                Reply("What do you want to rinse?");
                return;
            }

            if (thingIds.Intersect(Plates).Count() > 0)
            {
                RinsePlate();
            }
            else
            {
                Reply("You can't rinse that.");
            }
        }

        #endregion

        #region Display helpers

        /// <summary>
        /// Displays everything the player needs to know about the location.
        /// </summary>
        static void DisplayLocation(bool forceShowDescription)
        {
            // Display current location description.
            LocationData currentLocationData = LocationsData[CurrentLocationId];

            Console.Clear();

            // If we've already visited this location, show just the name, unless we explicitely asked for the description.
            if (LocationSeen[CurrentLocationId] && !forceShowDescription)
            {
                Print($"{currentLocationData.Name}.");
            }
            else
            {
                Print(currentLocationData.Description);

                // Mark that we've seen this location's description.
                LocationSeen[CurrentLocationId] = true;
            }

            Print();

            // Display possible directions.
            String directionsDescription = "Possible exits are:";

            foreach (KeyValuePair<Direction, LocationId> directionEntry in currentLocationData.Directions)
            {
                string directionName = directionEntry.Key.ToString().ToLowerInvariant();
                directionsDescription += $" {directionName}";
            }

            Print(directionsDescription + ".");

            // Display things that are at the location.
            Print("You see:");

            IEnumerable<ThingId> thingsAtCurrentLocation = GetThingsAtLocation(CurrentLocationId);

            if (thingsAtCurrentLocation.Count() == 0)
            {
                Print("    nothing.");
            }
            foreach (ThingId thingId in thingsAtCurrentLocation)
            {
                Print($"    {GetName(thingId)}.");
            }

            Print();
        }

        /// <summary>
        /// Displays the specified thing's description.
        /// </summary>
        static void DisplayThing(ThingId thing)
        {
            // Checklist requires special handling.
            if (thing == ThingId.Checklist)
            {
                DisplayChecklist();
                return;
            }

            ThingData thingData = ThingsData[thing];
            Reply(thingData.Description);
        }

        /// <summary>
        /// Displays which goals you have to achieve.
        /// </summary>
        static void DisplayChecklist()
        {
            Print("You go over the mental checklist of things you're supposed to do today:");

            var goalDescriptions = new Dictionary<Goal, string>()
            {
                { Goal.SuitcaseInRoom, "Drop suitcase in your room." },
                { Goal.ComputerInOffice, "Find a desk and put your computer on it." },
                { Goal.DinnerEaten, "Eat dinner." },
                { Goal.ShowerTaken, "Take a shower." }
            };

            foreach (KeyValuePair<Goal, string> goalDescription in goalDescriptions)
            {
                string descriptionLine = $"    {goalDescription.Value}";

                // Change color and append a checkmark if the goal has been reached.
                if (GoalCompleted[goalDescription.Key])
                {
                    Console.ForegroundColor = SuccessColor;
                    descriptionLine += " ✔";
                }

                Print(descriptionLine);

                Console.ForegroundColor = NarrativeColor;
            }

            Print();
        }

        /// <summary>
        /// Displays things in the inventory.
        /// </summary>
        static void DisplayInventory()
        {
            Print("You are carrying:");

            IEnumerable<ThingId> thingsInInventory = GetThingsAtLocation(LocationId.Inventory);

            if (thingsInInventory.Count() == 0)
            {
                Print("    nothing.");
            }
            else
            {
                foreach (ThingId thingId in thingsInInventory)
                {
                    Print($"    {GetName(thingId)}.");
                }
            }

            Print();
        }

        #endregion

        #region Event helpers

        // Condition helpers

        /// <summary>
        /// Tells if the thing is at the specified location.
        /// </summary>
        static bool ThingAt(ThingId thingId, LocationId locationId)
        {
            if (!ThingsLocations.ContainsKey(thingId)) return false;
            return ThingsLocations[thingId] == locationId;
        }

        /// <summary>
        /// Tells if the thing is at the current location.
        /// </summary>
        static bool ThingIsHere(ThingId thingId)
        {
            return ThingAt(thingId, CurrentLocationId);
        }

        /// <summary>
        /// Tells if the thing is either at the current location or in the inventory.
        /// </summary>
        static bool ThingAvailable(ThingId thingId)
        {
            return ThingIsHere(thingId) || HaveThing(thingId);
        }

        /// <summary>
        /// Tells if the thing is in the inventory.
        /// </summary>
        static bool HaveThing(ThingId thing)
        {
            return ThingAt(thing, LocationId.Inventory);
        }

        // Action helpers

        /// <summary>
        /// Moves thing to desired location.
        /// </summary>
        static void MoveThing(ThingId thingId, LocationId locationId)
        {
            ThingsLocations[thingId] = locationId;
        }

        /// <summary>
        /// Swaps the locations between the pair of specified things.
        /// </summary>
        /// <param name="thing"></param>
        /// <param name="thing"></param>
        static void SwapThings(ThingId thing1Id, ThingId thing2Id)
        {
            LocationId location1Id = ThingsLocations[thing1Id];
            LocationId location2Id = ThingsLocations[thing2Id];

            MoveThing(thing1Id, location2Id);
            MoveThing(thing2Id, location1Id);
        }

        /// <summary>
        /// Places the thing into the inventory.
        /// </summary>
        static bool GetThing(ThingId thingId)
        {
            // Meatballs can't be taken without a plate.
            if (thingId == ThingId.Meatballs && !(HaveThing(ThingId.CleanPlate) || HaveThing(ThingId.DirtyPlate)))
            {
                Reply("You should get a plate first.");
                return false;
            }

            MoveThing(thingId, LocationId.Inventory);
            return true;
        }

        /// <summary>
        /// Places the thing to the current location.
        /// </summary>
        static bool DropThing(ThingId thing)
        {
            // Meatballs shouldn't be dropped.
            if (thing == ThingId.Meatballs)
            {
                Reply("You shouldn't be throwing food away!");
                return false;
            }

            // Plate while having meatballs shouldn't be dropped.
            if (Plates.Contains(thing) && HaveThing(ThingId.Meatballs))
            {
                Reply("You still have meatballs to eat!");
                return false;
            }

            MoveThing(thing, CurrentLocationId);
            return true;
        }

        #endregion

        #region Events

        static void TalkToJames()
        {
            // You can only talk to James while he's in the lobby.
            if (ThingAt(ThingId.James, LocationId.Lobby))
            {
                Reply("James says \"Welcome to Spelkollektivet! You'll first want to get settled in. Your room is on the west side of the north wing.\"");
                Reply("James points to the east where the north wing starts. He adds \"If you have any questions, I'll be in the reception.\"");
                Reply("James leaves southeast.");

                MoveThing(ThingId.James, LocationId.Reception);
            }
            else
            {
                Reply("James looks busy and you don't want to bother him.");
            }
        }

        static void OpenSuitcase()
        {
            // Make sure you have the suitcase.
            if (!ThingAvailable(ThingId.Suitcase))
            {
                Reply("Hmm … Where did you put your suitcase?");
                return;
            }

            // Place the computer at the suitcase's location.
            MoveThing(ThingId.Computer, ThingsLocations[ThingId.Suitcase]);

            Reply("You open the suitcase and see your computer in it.");
        }

        static void TakeShower()
        {
            // You can only take a shower if it's there.
            if (!ThingIsHere(ThingId.Shower))
            {
                Reply("I don't see a shower here. Maybe try a bathroom?");
                return;
            }

            // You shouldn't shower with certain items present.
            foreach (ThingId thingId in new[] { ThingId.Suitcase, ThingId.Computer })
            {
                if (ThingAvailable(thingId))
                {
                    Reply($"You shouldn't shower with your {GetName(thingId)} around.");
                    return;
                }
            }

            Reply("You turn on the water and enjoy a long, hot shower.");
            Reply("After you're done, there's water all over the floor. You also left a souvenier of hair in the drain.");

            // Add things to clean to the location.
            DropThing(ThingId.Hair);
            DropThing(ThingId.Puddle);

            // Goal of showering is completed.
            GoalCompleted[Goal.ShowerTaken] = true;
        }

        static void MopPuddle()
        {
            // Make sure we're next to a puddle.
            if (!ThingIsHere(ThingId.Puddle))
            {
                Reply("There aren't any puddles of water here.");
                return;
            }

            // We need the mop to mop the puddle.
            if (!HaveThing(ThingId.Mop))
            {
                Reply("Try grabbing a mop first.");
                return;
            }

            Reply("You grip the mop firmly and drag it tightly across the floor towards the drain.");
            Reply("The floor is now dry and you feel good about yourself.");

            MoveThing(ThingId.Puddle, LocationId.Nowhere);
        }

        static void ThrowInTrash(IEnumerable<ThingId> thingIds)
        {
            // Make sure we're throwing away something.
            if (thingIds.Count() == 0)
            {
                Reply("I don't know what you want to throw in the trash.");
                return;
            }

            foreach (ThingId thingId in thingIds)
            {
                // Make sure you have the thing.
                if (!HaveThing(thingId))
                {
                    Reply($"{Capitalize(GetName(thingId))} is not in your inventory.");
                    continue;
                }

                // We can only throw away hair.
                if (thingId != ThingId.Hair)
                {
                    Reply($"I don't want to throw the {GetName(thingId)} away!");
                    continue;
                }

                Reply("You dispose your hair into the trash bin. Humanity thanks you!");

                MoveThing(ThingId.Hair, LocationId.Nowhere);
            }
        }

        static void Eat()
        {
            // Make sure we have meatballs.
            if (!HaveThing(ThingId.Meatballs))
            {
                Reply("You don't have anything to eat.");
                return;
            }

            Reply("You eat the delicious meatballs and are immediately content with the decision of moving into this house. The food will be one of unexpected highlights of living here.");

            MoveThing(ThingId.Meatballs, LocationId.Nowhere);

            GoalCompleted[Goal.DinnerEaten] = true;
        }

        static void RinsePlate()
        {
            // You have to be in a room with a sink to rinse dishes.
            if (!new[] { LocationId.Scullery, LocationId.HomiesKitchen, LocationId.NorthWingBathroom }.Contains(CurrentLocationId))
            {
                Reply("There is no sink here.");
                return;
            }

            // See if we have a plate.
            if (ThingAvailable(ThingId.CleanPlate))
            {
                Reply("The plate is already clean.");
                return;
            }
            else if (ThingAvailable(ThingId.RinsedPlate))
            {
                Reply("The plate is already rinsed.");
                return;
            }
            else if (!ThingAvailable(ThingId.DirtyPlate))
            {
                Reply("You don't have a plate to rinse.");
                return;
            }

            Reply("As a good future homie, you rinse the plate so that the dishwasher will have an easier time getting it super clean.");

            // The plate is now rinsed.
            SwapThings(ThingId.DirtyPlate, ThingId.RinsedPlate);

            // We also need the word plate to refer to the rinsed plate now.
            ThingIdsByName["plate"] = ThingId.RinsedPlate;
        }

        static void HandleSleep()
        {
            // You have to be in your room to sleep.
            if (CurrentLocationId != LocationId.YourRoom)
            {
                Reply("Maybe try sleeping in your room?");
                return;
            }

            // Make sure all the goals are completed.
            if (!AllGoalsCompleted())
            {
                Reply("You still have things to do today. Look at your checklist!");
                return;
            }

            EndGame();
        }

        #endregion

        #region Game rules

        static void ApplyGameRules()
        {
            // Handle goals.
            GoalCompleted[Goal.SuitcaseInRoom] = ThingAt(ThingId.Suitcase, LocationId.YourRoom);

            GoalCompleted[Goal.ComputerInOffice] = ThingAt(ThingId.Computer, LocationId.LoudOffice);

            // Dropping your computer in the loud office claims the desk.
            if (ThingAt(ThingId.Computer, LocationId.LoudOffice) && ThingAt(ThingId.EmptyDesk, LocationId.LoudOffice))
            {
                SwapThings(ThingId.EmptyDesk, ThingId.YourDesk);

                // We also need the word desk to refer to your desk now.
                ThingIdsByName["desk"] = ThingId.YourDesk;
            }

            // Getting meatballs dirties your plate.
            if (HaveThing(ThingId.Meatballs) && HaveThing(ThingId.CleanPlate))
            {
                // The plate is now dirty.
                SwapThings(ThingId.CleanPlate, ThingId.DirtyPlate);

                // We also need the word plate to refer to the dirty plate now.
                ThingIdsByName["plate"] = ThingId.DirtyPlate;
            }

            // When you complete all goals, the game should tell you to go to sleep.
            if (AllGoalsCompleted() && !SleepHintGiven)
            {
                Reply("Congratulations! You've completed all four goals. It's been a long day, so when you're ready to be evaluated, go to sleep in your room. Now is the chance for any last actions.");
                SleepHintGiven = true;
            }
        }

        static bool AllGoalsCompleted()
        {
            return GoalCompleted.All(goal => goal.Value);
        }

        static void EndGame()
        {
            Reply("Exhausted at the end of your first day, you doze off to sleep. You've completed all four goals, but … have you been a good homie at doing it?");

            Console.ReadKey();

            bool madeAnyMistakes = false;

            // Judge the showering situation.
            string hairFeedback;

            // Did you mop the floor?
            if (ThingAt(ThingId.Puddle, LocationId.Nowhere))
            {
                Reply("You took a shower and you mopped the floor afterwards. Awesome!");

                // Did you return the mop?
                if (!ThingAt(ThingId.Mop, LocationId.NorthWingBathroom))
                {
                    Reply("It would be nice if you also left the mop back in the bathroom for other homies to use afterwards.");
                    madeAnyMistakes = true;
                }

                // Did you get the hair?
                if (ThingAt(ThingId.Hair, LocationId.NorthWingBathroom))
                {
                    if (madeAnyMistakes)
                    {
                        hairFeedback = "You also left a ball of hair in the drain after you.";
                    }
                    else
                    {
                        hairFeedback = "However, you left a ball of hair in the drain after you.";
                    }

                    madeAnyMistakes = true;
                }
                else
                {
                    hairFeedback = "Thank you for picking up your hair from the drain as well.";
                }
            }
            else
            {
                Reply("You took a shower and you left a huge puddle of water all over the floor. Please use the mop and clean after yourself next time.");
                madeAnyMistakes = true;

                // Did you get the hair?
                if (ThingAt(ThingId.Hair, LocationId.NorthWingBathroom))
                {
                    hairFeedback = "You also left a ball of hair in the drain after you.";
                }
                else
                {
                    hairFeedback = "Thank you for picking up your hair from the drain as well.";
                }
            }

            // Add additional scolding if you left the hair.
            if (ThingAt(ThingId.Hair, LocationId.NorthWingBathroom))
            {
                hairFeedback += " Try to be mindful of the homies coming to shower after you and don't leave hairy souvenirs for them.";
            }
            else
            {
                // Is the hair still in your hands?
                if (HaveThing(ThingId.Hair))
                {
                    hairFeedback += " However, you were a bit gross running around with it in your hands all day.";
                    madeAnyMistakes = true;
                }
                // Did you drop it somwhere else?
                else if (!ThingAt(ThingId.Hair, LocationId.Nowhere))
                {
                    hairFeedback += "However, throwing it somewhere else is not a nice thing to do. Next time dispose of it in the trash.";
                    madeAnyMistakes = true;
                }
            }

            Reply(hairFeedback);

            Console.ReadKey();

            // Judge the dinner situation.
            var dinnerFeedback = "We hope the meatballs were delicious.";

            if (ThingAt(ThingId.DirtyPlate, LocationId.Nowhere))
            {
                dinnerFeedback += " Thank you for rinsing the plate after you";

                if (ThingAt(ThingId.RinsedPlate, LocationId.Scullery))
                {
                    dinnerFeedback += " and leaving it by the dishwasher to get it super clean. Great job!";
                }
                else
                {
                    dinnerFeedback += ". Next time also leave it by the dishwasher so it gets thoroughly cleaned as well.";
                    madeAnyMistakes = true;
                }
            }
            else
            {
                dinnerFeedback += " It would be nice, however, if you rinsed the dirty plate";
                madeAnyMistakes = true;

                // Did you at least leave it in the scullery?
                if (ThingAt(ThingId.DirtyPlate, LocationId.Scullery))
                {
                    dinnerFeedback += ". It's nice that you dropped it off at the dishwasher, but if thick layers of food are left on it, they sometimes don't get cleaned. Remember, nobody would like to eat your leftovers!";
                }
                else
                {
                    dinnerFeedback += " and placed it next to the dishwasher.";
                }
            }

            Reply(dinnerFeedback);

            Console.ReadKey();

            if (madeAnyMistakes)
            {
                Reply("We hope you've learned something today. It's not always easy to live in a house full of other people, but we can make it very enjoyable if we all take care of the place and keep things in the same condition as we found them.");
            }
            else
            {
                Reply("You've shown that you can get things done and be mindful of your fellow homies at the same time. You are a shining example of how to behave in a coliving environment. Have a wonderful night!");
            }

            Console.ReadKey();

            ShouldQuit = true;
        }

        #endregion
    }
}
