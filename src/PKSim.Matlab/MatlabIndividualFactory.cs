using System.Collections.Generic;
using System.Linq;
using OSPSuite.Utility.Container;
using PKSim.Core.Batch.Mapper;
using PKSim.Core.Mappers;
using PKSim.Core.Model;
using PKSim.Core.Model.Extensions;
using OSPSuite.Core.Domain;
using OSPSuite.Core.Domain.Services;
using OriginData = PKSim.Core.Batch.OriginData;

namespace PKSim.Matlab
{
   public interface IMatlabIndividualFactory
   {
      ParameterValue[] CreateIndividual(OriginData batchOriginData, IEnumerable<MoleculeOntogeny> moleculeOntogenies);
      DistributedParameterValue[] DistributionsFor(OriginData batchOriginData, IEnumerable<MoleculeOntogeny> moleculeOntogenies);
   }

   public class MatlabIndividualFactory : IMatlabIndividualFactory
   {
      private readonly IOriginDataMapper _originDataMapper;
      private readonly IIndividualFactory _individualFactory;
      private readonly IIndividualToIndividualPropertiesMapper _individualPropertiesMapper;
      private readonly IOntogenyFactorsRetriever _ontogenyFactorsRetriever;
      private readonly IEntityPathResolver _entityPathResolver;

      static MatlabIndividualFactory()
      {
         ApplicationStartup.Initialize();
      }

      public MatlabIndividualFactory()
         : this(IoC.Resolve<IOriginDataMapper>(), IoC.Resolve<IIndividualFactory>(), IoC.Resolve<IIndividualToIndividualPropertiesMapper>(),
                IoC.Resolve<IOntogenyFactorsRetriever>(), IoC.Resolve<IEntityPathResolver>())
      {
      }

      internal MatlabIndividualFactory(IOriginDataMapper originDataMapper, IIndividualFactory individualFactory, IIndividualToIndividualPropertiesMapper individualPropertiesMapper, IOntogenyFactorsRetriever ontogenyFactorsRetriever, IEntityPathResolver entityPathResolver)
      {
         _originDataMapper = originDataMapper;
         _individualFactory = individualFactory;
         _individualPropertiesMapper = individualPropertiesMapper;
         _ontogenyFactorsRetriever = ontogenyFactorsRetriever;
         _entityPathResolver = entityPathResolver;
      }

      public ParameterValue[] CreateIndividual(OriginData batchOriginData, IEnumerable<MoleculeOntogeny> moleculeOntogenies)
      {
         var originData = _originDataMapper.MapFrom(batchOriginData);
         var individual = _individualFactory.CreateAndOptimizeFor(originData);
         var individualProperties = _individualPropertiesMapper.MapFrom(individual);
         var allIndividualParameters = individualProperties.ParameterValues.ToList();
         allIndividualParameters.AddRange(_ontogenyFactorsRetriever.FactorsFor(originData, moleculeOntogenies));
         return allIndividualParameters.ToArray();
      }

      public DistributedParameterValue[] DistributionsFor(OriginData batchOriginData, IEnumerable<MoleculeOntogeny> moleculeOntogenies)
      {
         var originData = _originDataMapper.MapFrom(batchOriginData);
         var individual = _individualFactory.CreateAndOptimizeFor(originData);
         return individual.GetAllChildren<IDistributedParameter>().Select(distributedParameterValueFrom).ToArray();
      }

      private DistributedParameterValue distributedParameterValueFrom(IDistributedParameter parameter)
      {
         var parameterPath = _entityPathResolver.PathFor(parameter);
         var distributionType = parameter.Formula.DistributionType();
         double p1 = 0, p2 = 0;
         if (distributionType == DistributionTypes.Normal || distributionType == DistributionTypes.LogNormal)
         {
            p1 = parameter.MeanParameter.Value;
            p2 = parameter.DeviationParameter.Value;
         }
         else if (distributionType == DistributionTypes.Uniform)
         {
            p1 = parameter.Parameter(Constants.Distribution.MINIMUM).Value;
            p2 = parameter.Parameter(Constants.Distribution.MAXIMUM).Value;
         }
         return new DistributedParameterValue(parameterPath, parameter.Value, parameter.Percentile, p1, p2, distributionType);
      }
   }
} ;