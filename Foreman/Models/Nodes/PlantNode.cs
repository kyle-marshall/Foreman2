using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TreeView;

namespace Foreman
{
	public class PlantNode : BaseNode
	{
        public enum Errors
        {
            Clean = 0b_0000_0000_0000,
            ItemDoesntGrow = 0b_0000_0000_0001,
            InvalidGrowResult = 0b_0000_0000_0010,
			InputItemMissing = 0b_0000_0000_0100,
			PlantProcessMissing = 0b_0000_0000_1000,
            InvalidLinks = 0b_1000_0000_0000
        }

        public Errors ErrorSet { get; private set; }

        private readonly BaseNodeController controller;
		public override BaseNodeController Controller { get { return controller; } }

		public Item Seed { get { return BasePlantProcess.Seed; } }
		public PlantProcess BasePlantProcess { get; internal set; }

		public override IEnumerable<Item> Inputs { get { yield return Seed; } }
		public override IEnumerable<Item> Outputs { get { return BasePlantProcess.ProductList; } }

        //for plant nodes, the SetValue is 'number of plant tiles'
        public override double ActualSetValue { get { return ActualRatePerSec * Seed.PlantResult.GrowTime; } }
        public override double DesiredSetValue { get; set; }
        public override double MaxDesiredSetValue { get { return ProductionGraph.MaxTiles; } }
        public override string SetValueDescription { get { return "Number of farming tiles"; } }

        public override double DesiredRatePerSec { get { return DesiredSetValue / Seed.PlantResult.GrowTime; } }

        public PlantNode(ProductionGraph graph, int nodeID, Item item) : this(graph, nodeID, item.PlantResult) { }
		public PlantNode(ProductionGraph graph, int nodeID, PlantProcess plantProcess) : base(graph, nodeID)
        {
			BasePlantProcess = plantProcess;
			controller = PlantNodeController.GetController(this);
			ReadOnlyNode = new ReadOnlyPlantNode(this);
		}

		public override void UpdateState(bool makeDirty = true)
		{
			if (makeDirty)
				IsClean = false;
			NodeState oldState = State;
            ErrorSet = Errors.Clean;

			if (Seed.PlantResult == null)
				ErrorSet |= Errors.ItemDoesntGrow;
			if (Seed.PlantResult != BasePlantProcess)
				ErrorSet |= Errors.InvalidGrowResult;
			if (Seed.IsMissing)
				ErrorSet |= Errors.InputItemMissing;
			if  (BasePlantProcess.IsMissing)
				ErrorSet |= Errors.PlantProcessMissing;
			if (!AllLinksValid)
				ErrorSet |= Errors.InvalidLinks;

            State = (ErrorSet == Errors.Clean) ? AllLinksConnected ? NodeState.Clean : NodeState.MissingLink : NodeState.Error;
			if (oldState != State)
				OnNodeStateChanged();
		}

		public override double GetConsumeRate(Item item) { return ActualRate; }
		public override double GetSupplyRate(Item item) { return ActualRate * outputRateFor(item); }

		internal override double inputRateFor(Item item) { return 1; }
		internal override double outputRateFor(Item item) { return BasePlantProcess.ProductSet[item]; }

		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);

			info.AddValue("NodeType", NodeType.Plant);
			info.AddValue("PlantProcessID", BasePlantProcess.PlantID);
		}

		public override string ToString() { return string.Format("Plant Growth node for: {0}", Seed.Name); }
	}

	public class ReadOnlyPlantNode : ReadOnlyBaseNode
	{
		public Item Seed => MyNode.Seed;
		public PlantProcess SeedPlantProcess => MyNode.BasePlantProcess;

		private readonly PlantNode MyNode;

		public ReadOnlyPlantNode(PlantNode node) : base(node) { MyNode = node; }

		public override List<string> GetErrors()
		{
            PlantNode.Errors ErrorSet = MyNode.ErrorSet;
            List<string> errors = new List<string>();

			if ((ErrorSet & PlantNode.Errors.InputItemMissing) != 0)
				errors.Add(string.Format("> Item \"{0}\" doesnt exist in preset!", Seed.FriendlyName));
            if ((ErrorSet & PlantNode.Errors.PlantProcessMissing) != 0)
                errors.Add(string.Format("> Growth process for item \"{0}\" doesnt exist in preset!", Seed.FriendlyName));
			if((ErrorSet & PlantNode.Errors.ItemDoesntGrow) != 0)
                errors.Add(string.Format("> Item \"{0}\" cant be planted!", Seed.FriendlyName));
            if ((ErrorSet & PlantNode.Errors.InvalidGrowResult) != 0)
                errors.Add(string.Format("> Growth result for item \"{0}\" doesnt match preset!", Seed.FriendlyName));
            if ((ErrorSet & PlantNode.Errors.InvalidLinks) != 0)
				errors.Add("> Some links are invalid!");
			return errors;
		}

		public override List<string> GetWarnings() { Trace.Fail("Spoil node never has the warning state!"); return null; }
    }

	public class PlantNodeController : BaseNodeController
	{
		private readonly PlantNode MyNode;

		protected PlantNodeController(PlantNode myNode) : base(myNode) { MyNode = myNode; }

		public static PlantNodeController GetController(PlantNode node)
		{
			if (node.Controller != null)
				return (PlantNodeController)node.Controller;
			return new PlantNodeController(node);
		}

        public void UpdatePlantResult()
        {
			if(MyNode.BasePlantProcess != MyNode.Seed.PlantResult)
			{
				MyNode.BasePlantProcess = MyNode.Seed.PlantResult;
				foreach(NodeLink link in MyNode.OutputLinks.Where(l => !MyNode.BasePlantProcess.ProductList.Contains(l.Item)))
					link.Controller.Delete();
				MyNode.UpdateState();
			}
        }

        public override Dictionary<string, Action> GetErrorResolutions()
		{
			Dictionary<string, Action> resolutions = new Dictionary<string, Action>();
			if ((MyNode.ErrorSet & (PlantNode.Errors.InputItemMissing | PlantNode.Errors.PlantProcessMissing | PlantNode.Errors.ItemDoesntGrow)) != 0)
				resolutions.Add("Delete node", new Action(() => this.Delete()));
			if ((MyNode.ErrorSet & PlantNode.Errors.InvalidGrowResult) != 0)
				resolutions.Add("Update plant results", new Action(() => UpdatePlantResult()));
			else
				foreach (KeyValuePair<string, Action> kvp in GetInvalidConnectionResolutions())
					resolutions.Add(kvp.Key, kvp.Value);
			return resolutions;
		}

		public override Dictionary<string, Action> GetWarningResolutions() { Trace.Fail("Plant node never has the warning state!"); return null; }
    }
}
