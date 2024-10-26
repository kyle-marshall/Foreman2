using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TreeView;

namespace Foreman
{
	public class SpoilNode : BaseNode
	{
        public enum Errors
        {
            Clean = 0b_0000_0000_0000,
            ItemDoesntSpoil = 0b_0000_0000_0001,
            InvalidSpoilResult = 0b_0000_0000_0010,
			InputItemMissing = 0b_0000_0000_0100,
			OutputItemMissing = 0b_0000_0000_1000,
            InvalidLinks = 0b_1000_0000_0000
        }

        public Errors ErrorSet { get; private set; }

        private readonly BaseNodeController controller;
		public override BaseNodeController Controller { get { return controller; } }

		public readonly Item InputItem;
		public Item OutputItem { get; internal set; }

		public override IEnumerable<Item> Inputs { get { yield return InputItem; } }
		public override IEnumerable<Item> Outputs { get { yield return OutputItem; } }

        //for spoil nodes, the SetValue is 'number of stacks (item slots)'
        public override double ActualSetValue { get { return ActualRatePerSec * InputItem.GetItemSpoilageTime(InputItem.Owner.DefaultQuality) / InputItem.StackSize; } }  //QUALITY UPDATE REQUIRED
        public override double DesiredSetValue { get; set; }
        public override double MaxDesiredSetValue { get { return ProductionGraph.MaxInventorySlots; } }
        public override string SetValueDescription { get { return "Number of inventory slots"; } }

        public override double DesiredRatePerSec { get { return DesiredSetValue * InputItem.StackSize / InputItem.GetItemSpoilageTime(InputItem.Owner.DefaultQuality); } } //QUALITY UPDATE REQUIRED

        public SpoilNode(ProductionGraph graph, int nodeID, Item item) : this(graph, nodeID, item, item.SpoilResult) { }
		public SpoilNode(ProductionGraph graph, int nodeID, Item item, Item outputItem) : base(graph, nodeID)
        {
			InputItem = item;
			OutputItem = outputItem;
			controller = SpoilNodeController.GetController(this);
			ReadOnlyNode = new ReadOnlySpoilNode(this);
		}

		public override void UpdateState(bool makeDirty = true)
		{
			if (makeDirty)
				IsClean = false;
			NodeState oldState = State;
            ErrorSet = Errors.Clean;

			if (InputItem.SpoilResult == null)
				ErrorSet |= Errors.ItemDoesntSpoil;
			if (InputItem.SpoilResult != OutputItem)
				ErrorSet |= Errors.InvalidSpoilResult;
			if (InputItem.IsMissing)
				ErrorSet |= Errors.InputItemMissing;
			if  (OutputItem.IsMissing)
				ErrorSet |= Errors.OutputItemMissing;
			if (!AllLinksValid)
				ErrorSet |= Errors.InvalidLinks;

            State = (ErrorSet == Errors.Clean) ? AllLinksConnected ? NodeState.Clean : NodeState.MissingLink : NodeState.Error;
			if (oldState != State)
				OnNodeStateChanged();
		}

		public override double GetConsumeRate(Item item) { return ActualRate; }
		public override double GetSupplyRate(Item item) { return ActualRate; }

		internal override double inputRateFor(Item item) { return 1; }
		internal override double outputRateFor(Item item) { return 1; }

		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);

			info.AddValue("NodeType", NodeType.Spoil);
			info.AddValue("InputItem", InputItem.Name);
			info.AddValue("OutputItem", OutputItem.Name);
		}

		public override string ToString() { return string.Format("Spoil node for: {0} to {1}", InputItem.Name, OutputItem.Name); }
	}

	public class ReadOnlySpoilNode : ReadOnlyBaseNode
	{
		public Item InputItem => MyNode.InputItem;
		public Item OutputItem => MyNode.OutputItem;

		private readonly SpoilNode MyNode;

        public ReadOnlySpoilNode(SpoilNode node) : base(node) { MyNode = node; }

		public override List<string> GetErrors()
		{
            SpoilNode.Errors ErrorSet = MyNode.ErrorSet;
            List<string> errors = new List<string>();

			if ((ErrorSet & SpoilNode.Errors.InputItemMissing) != 0)
				errors.Add(string.Format("> Item \"{0}\" doesnt exist in preset!", InputItem.FriendlyName));
            if ((ErrorSet & SpoilNode.Errors.OutputItemMissing) != 0)
                errors.Add(string.Format("> Spoilage Item \"{0}\" doesnt exist in preset!", OutputItem.FriendlyName));
			if((ErrorSet & SpoilNode.Errors.ItemDoesntSpoil) != 0)
                errors.Add(string.Format("> Item \"{0}\" doesnt spoil!", InputItem.FriendlyName));
            if ((ErrorSet & SpoilNode.Errors.InvalidSpoilResult) != 0)
                errors.Add(string.Format("> Spoilage Item \"{0}\" doesnt exist in preset!", OutputItem.FriendlyName));
            if ((ErrorSet & SpoilNode.Errors.InvalidLinks) != 0)
				errors.Add("> Some links are invalid!");
			return errors;
		}

		public override List<string> GetWarnings() { Trace.Fail("Spoil node never has the warning state!"); return null; }
    }

	public class SpoilNodeController : BaseNodeController
	{
		private readonly SpoilNode MyNode;

		protected SpoilNodeController(SpoilNode myNode) : base(myNode) { MyNode = myNode; }

		public static SpoilNodeController GetController(SpoilNode node)
		{
			if (node.Controller != null)
				return (SpoilNodeController)node.Controller;
			return new SpoilNodeController(node);
		}

        public void UpdateSpoilResult()
        {
            Item correctSpoilResult = MyNode.InputItem.SpoilResult;
            if (MyNode.OutputItem.SpoilResult != correctSpoilResult)
            {
                foreach (NodeLink link in MyNode.OutputLinks)
                    link.Controller.Delete();
                MyNode.OutputItem = correctSpoilResult;
                MyNode.UpdateState();
            }
        }

        public override Dictionary<string, Action> GetErrorResolutions()
		{
			Dictionary<string, Action> resolutions = new Dictionary<string, Action>();
			if ((MyNode.ErrorSet & (SpoilNode.Errors.InputItemMissing | SpoilNode.Errors.OutputItemMissing | SpoilNode.Errors.ItemDoesntSpoil)) != 0)
				resolutions.Add("Delete node", new Action(() => this.Delete()));
			if ((MyNode.ErrorSet & SpoilNode.Errors.InvalidSpoilResult) != 0)
				resolutions.Add("Update spoil result", new Action(() => UpdateSpoilResult()));
			else
				foreach (KeyValuePair<string, Action> kvp in GetInvalidConnectionResolutions())
					resolutions.Add(kvp.Key, kvp.Value);
			return resolutions;
		}

		public override Dictionary<string, Action> GetWarningResolutions() { Trace.Fail("Spoil node never has the warning state!"); return null; }
    }
}
