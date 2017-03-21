﻿using OSPSuite.Utility;
using OSPSuite.Utility.Extensions;
using PKSim.Core.Model;
using PKSim.Core.Services;
using OSPSuite.Core.Domain;
using OSPSuite.Core.Services;

namespace PKSim.Core.Batch.Mapper
{
   public interface IIndividualMapper : IMapper<Individual, Model.Individual>
   {
   }

   public class IndividualMapper : IIndividualMapper
   {
      private readonly IIndividualFactory _individualFactory;
      private readonly IOriginDataMapper _originDataMapper;
      private readonly IIndividualMoleculeFactoryResolver _moleculeFactoryResolver;
      private readonly IBatchLogger _batchLogger;

      public IndividualMapper(IIndividualFactory individualFactory, IOriginDataMapper originDataMapper,
         IIndividualMoleculeFactoryResolver moleculeFactoryResolver, IBatchLogger batchLogger)
      {
         _individualFactory = individualFactory;
         _originDataMapper = originDataMapper;
         _moleculeFactoryResolver = moleculeFactoryResolver;
         _batchLogger = batchLogger;
      }

      public Model.Individual MapFrom(Individual batchIndividual)
      {
         var batchOriginData = new OriginData
         {
            Species = batchIndividual.Species,
            Population = batchIndividual.Population,
            Gender = batchIndividual.Gender,
            Age = batchIndividual.Age.GetValueOrDefault(double.NaN),
            Height = batchIndividual.Height.GetValueOrDefault(double.NaN),
            Weight = batchIndividual.Weight.GetValueOrDefault(double.NaN),
            GestationalAge = batchIndividual.GestationalAge.GetValueOrDefault(double.NaN)
         };

         var originData = _originDataMapper.MapFrom(batchOriginData);

         Model.Individual individual;
         if (batchIndividual.Optimize)
            individual = _individualFactory.CreateAndOptimizeFor(originData);
         else
            individual = _individualFactory.CreateStandardFor(originData);

         individual.Name = "Individual";

         batchIndividual.Enzymes.Each(enzyme => addMoleculeTo<IndividualEnzyme>(individual, enzyme));
         batchIndividual.OtherProteins.Each(otherProtein => addMoleculeTo<IndividualOtherProtein>(individual, otherProtein));
         batchIndividual.Transporters.Each(transporter =>
         {
            var individualTransporter = addMoleculeTo<IndividualTransporter>(individual, transporter);
            individualTransporter.TransportType = EnumHelper.ParseValue<TransportType>(transporter.TransportType);
            _batchLogger.AddDebug("Transport type for transporter '{0}' is {1}".FormatWith(individualTransporter.Name, individualTransporter.TransportType));
         });

         return individual;
      }

      private TMolecule addMoleculeTo<TMolecule>(Model.Individual individual, Molecule molecule) where TMolecule : IndividualMolecule
      {
         var proteinFactory = _moleculeFactoryResolver.FactoryFor<TMolecule>();
         var newMolecule = proteinFactory.CreateFor(individual);
         newMolecule.ReferenceConcentration.Value = molecule.ReferenceConcentration;
         newMolecule.HalfLifeLiver.Value = molecule.HalfLifeLiver;
         newMolecule.HalfLifeIntestine.Value = molecule.HalfLifeIntestine;
         newMolecule.Name = molecule.Name;
         individual.AddMolecule(newMolecule);

         foreach (var expression in molecule.Expressions)
         {
            var expressionParameter = newMolecule.GetRelativeExpressionNormParameterFor(expression.Key);
            if (expressionParameter == null)
            {
               _batchLogger.AddWarning("Relative Expression container '{0}' not found for '{1}'".FormatWith(expression.Key, molecule.Name));
               continue;
            }

            expressionParameter.Value = expression.Value;
            _batchLogger.AddDebug("Relative Expression norm for container '{0}' set to {1}".FormatWith(expression.Key, expression.Value));
         }
         return newMolecule.DowncastTo<TMolecule>();
      }
   }
}