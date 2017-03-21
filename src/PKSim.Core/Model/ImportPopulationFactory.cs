﻿using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PKSim.Assets;
using OSPSuite.Utility.Events;
using PKSim.Core.Services;
using OSPSuite.Core.Domain;
using OSPSuite.Core.Domain.Services;
using OSPSuite.Core.Services;

namespace PKSim.Core.Model
{
   public interface IImportPopulationFactory
   {
      /// <summary>
      ///    Create a population importing the data from the specified list of <paramref name="files" />. The
      ///    <paramref
      ///       name="cancellationToken" />
      ///    allows the caller to cancel the task
      /// </summary>
      Task<ImportPopulation> CreateFor(IReadOnlyCollection<string> files, Individual individual, CancellationToken cancellationToken);
   }

   public class ImportPopulationFactory : IImportPopulationFactory
   {
      private readonly IObjectBaseFactory _objectBaseFactory;
      private readonly IProgressManager _progressManager;
      private readonly IIndividualPropertiesCacheImporter _individualPropertiesCacheImporter;
      private readonly ICloner _cloner;
      private readonly IContainerTask _containerTask;
      private readonly IAdvancedParameterFactory _advancedParameterFactory;
      private PathCache<IParameter> _allParameters;
      private PathCache<IParameter> _allCreateIndividualParameters;

      public ImportPopulationFactory(IObjectBaseFactory objectBaseFactory, IProgressManager progressManager,
         IIndividualPropertiesCacheImporter individualPropertiesCacheImporter, ICloner cloner, IContainerTask containerTask, IAdvancedParameterFactory advancedParameterFactory)
      {
         _objectBaseFactory = objectBaseFactory;
         _progressManager = progressManager;
         _individualPropertiesCacheImporter = individualPropertiesCacheImporter;
         _cloner = cloner;
         _containerTask = containerTask;
         _advancedParameterFactory = advancedParameterFactory;
      }

      public async Task<ImportPopulation> CreateFor(IReadOnlyCollection<string> files, Individual individual, CancellationToken cancellationToken)
      {
         try
         {
            using (var progressUpdater = _progressManager.Create())
            {
               var importPopulation = createPopulationFor(individual);
               var popIndiviudal = importPopulation.Settings.BaseIndividual;
               _allParameters = _containerTask.CacheAllChildren<IParameter>(popIndiviudal);
               _allCreateIndividualParameters = _containerTask.CacheAllChildrenSatisfying<IParameter>(popIndiviudal, x => x.IsChangedByCreateIndividual);

               var settings = importPopulation.Settings;
               progressUpdater.Initialize(files.Count, PKSimConstants.UI.CreatingPopulation);

               //Create the new task and start the import using to list
               var tasks = files.Select(f => importFiles(f, cancellationToken)).ToList();

               // Add a loop to process the tasks one at a time until none remain. 
               while (tasks.Count > 0)
               {
                  cancellationToken.ThrowIfCancellationRequested();

                  // Identify the first task that completes.
                  var firstFinishedTask = await Task.WhenAny(tasks);

                  // Remove the selected task from the list so that you don't 
                  // process it more than once.
                  tasks.Remove(firstFinishedTask);

                  // Await the completed task. 
                  var importResult = await firstFinishedTask;

                  settings.AddFile(importResult.PopulationFile);
                  mergeImportedIndividualsInPopulation(importPopulation, importResult.IndividualValues);
                  progressUpdater.IncrementProgress();
               }

               //once all individuals have been imported, we need to create advanced parameters
               createAdvancedParametersFor(importPopulation);
               return importPopulation;
            }
         }
         finally
         {
            _allCreateIndividualParameters.Clear();
            _allParameters.Clear();
         }
      }

      private void createAdvancedParametersFor(ImportPopulation importPopulation)
      {
         foreach (var parameterPath in importPopulation.IndividualPropertiesCache.AllParameterPaths())
         {
            if (_allCreateIndividualParameters.Contains(parameterPath))
               continue;

            var advancedParameter = _advancedParameterFactory.Create(_allParameters[parameterPath], DistributionTypes.Unknown);

            //do not generate random values as these were loaded from files
            importPopulation.AddAdvancedParameter(advancedParameter, generateRandomValues: false);
         }
      }

      private void mergeImportedIndividualsInPopulation(ImportPopulation importPopulation, IndividualPropertiesCache individualValues)
      {
         importPopulation.IndividualPropertiesCache.Merge(individualValues, _allParameters);
      }

      private Task<ImportResult> importFiles(string file, CancellationToken cancellationToken)
      {
         return Task.Run(() =>
         {
            var populationFile = new PopulationFile {FilePath = file};

            var importResult = new ImportResult {IndividualValues = _individualPropertiesCacheImporter.ImportFrom(file, populationFile)};
            cancellationToken.ThrowIfCancellationRequested();

            validate(file, importResult.IndividualValues, populationFile);
            populationFile.NumberOfIndividuals = importResult.IndividualValues.Count;
            importResult.PopulationFile = populationFile;

            return importResult;
         }, cancellationToken);
      }

      private void validate(string file, IndividualPropertiesCache individualValues, IImportLogger logger)
      {
         foreach (var parameterPath in individualValues.AllParameterPaths().ToList())
         {
            if (_allParameters.Contains(parameterPath))
               continue;

            logger.AddWarning(PKSimConstants.Warning.ParameterWithPathNotFoundInBaseIndividual(parameterPath));
            individualValues.Remove(parameterPath);
         }
      }

      private class ImportResult
      {
         public IndividualPropertiesCache IndividualValues { get; set; }
         public PopulationFile PopulationFile { get; set; }
      }

      private ImportPopulation createPopulationFor(Individual individual)
      {
         var importPopulation = _objectBaseFactory.Create<ImportPopulation>();
         importPopulation.Root = _objectBaseFactory.Create<IRootContainer>();
         importPopulation.SetAdvancedParameters(_objectBaseFactory.Create<IAdvancedParameterCollection>());
         importPopulation.Settings.BaseIndividual = _cloner.Clone(individual);
         importPopulation.IsLoaded = true;
         return importPopulation;
      }
   }
}