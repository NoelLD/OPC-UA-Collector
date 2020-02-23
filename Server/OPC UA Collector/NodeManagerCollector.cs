﻿/* ========================================================================
 * Copyright (c) 2005-2016 The OPC Foundation, Inc. All rights reserved.
 *
 * OPC Foundation MIT License 1.00
 * 
 * Permission is hereby granted, free of charge, to any person
 * obtaining a copy of this software and associated documentation
 * files (the "Software"), to deal in the Software without
 * restriction, including without limitation the rights to use,
 * copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the
 * Software is furnished to do so, subject to the following
 * conditions:
 * 
 * The above copyright notice and this permission notice shall be
 * included in all copies or substantial portions of the Software.
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
 * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
 * OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
 * NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
 * HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
 * WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
 * OTHER DEALINGS IN THE SOFTWARE.
 *
 * The complete license agreement can be found here:
 * http://opcfoundation.org/License/MIT/1.00/
 * ======================================================================*/

using System;
using System.Collections.Generic;
using System.Xml;
using System.Threading;
using Opc.Ua;
using Opc.Ua.Server;
using System.IO;
using System.Reflection;
// Namespaces Zum Zugriff auf die SQLServer-Datenbank
using System.Data.SqlClient;
using System.Diagnostics;
using ServerCollector;


namespace ServerCollector
{
    /// <summary>
    /// A node manager the diagnostic information exposed by the server.
    /// </summary>
    public class NodeManagerCollector : CustomNodeManager2
    {
        #region Constructors
        /// <summary>
        /// Initializes the node manager.
        /// </summary>
        public NodeManagerCollector(IServerInternal server, ApplicationConfiguration configuration)
        :
            // base(server, configuration, Namespaces.Demo)
            base(server, configuration, ServerCollector.Namespaces.Collector)
        {
            SystemContext.NodeIdFactory = this;

            //m_configuration = configuration.ParseExtension<DemoConfiguration>();
            //if (m_configuration == null)
            //{
            //    m_configuration = new DemoConfiguration();
            //}

        }
        #endregion

        #region INodeIdFactory Members
        /// <summary>
        /// Creates the NodeId for the specified node.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="node">The node.</param>
        /// <returns>The new NodeId.</returns>
        public override NodeId New(ISystemContext context, NodeState node)
        {
            // uint id = Utils.IncrementIdentifier(ref m_lastUsedId);
            // return new NodeId(id, m_namespaceIndex);

            return node.NodeId;
        }
        #endregion


        #region INodeManager Members
        /// <summary>
        /// Does any initialization required before the address space can be used.
        /// </summary>
        /// <remarks>
        /// The externalReferences is an out parameter that allows the node manager to link to nodes
        /// in other node managers. For example, the 'Objects' node is managed by the CoreNodeManager and
        /// should have a reference to the root folder node(s) exposed by this node manager.  
        /// </remarks>
        public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
        {
            lock (Lock)
            {
                // ensure the process object can be found via the server object. 
                IList<IReference> references = null;

                if (!externalReferences.TryGetValue(ObjectIds.ObjectsFolder, out references))
                {
                    externalReferences[ObjectIds.ObjectsFolder] = references = new List<IReference>();
                }

                try
                {
                    NodeStateCollection nodes = ImportXml(externalReferences, "../../References/ServerCollector.xml");
                    fillCollector(nodes);
                }
                catch (FileNotFoundException exc)
                {
                    Debug.WriteLine("can't opern file ! ", exc.Message);
                }

            }
        }
        #region Controller
        private void fillCollector(NodeStateCollection nodes)
        {
            if (nodes.Equals(null))
            {
                createCollectorObject();
                return;
            }

            Dictionary<NodeId, NodeState> nodeDic = new Dictionary<NodeId, NodeState>();
            foreach (NodeState node in nodes)
            {
                nodeDic.Add(node.NodeId, node);
            }
            NodeState Controller = nodeDic[new NodeId((int)Collector.ObjectsIDs.ObjectController, NamespaceIndex)];

            //properties für die addNamespace methode

            Collector.ObjectsIDs[] methodsid = { 
                Collector.ObjectsIDs.ObjectControlleraddNamespace, 
                Collector.ObjectsIDs.ObjectControlleraddObjectNode, 
                Collector.ObjectsIDs.ObjectControllergetObjectRootNode, 
                Collector.ObjectsIDs.ObjectControllergetToken
            };
            foreach (Collector.ObjectsIDs n in methodsid)
            {
                GenericMethodCalledEventHandler m;
                MethodState addNamespaceMethod = (MethodState)nodeDic[new NodeId((ushort)n, NamespaceIndex)];
                NodeState input = nodeDic[new NodeId((ushort)(n + 1), NamespaceIndex)];
                NodeState output = nodeDic[new NodeId((ushort)(n + 2), NamespaceIndex)];
                switch (n)
                {
                    case Collector.ObjectsIDs.ObjectControlleraddNamespace:
                        m = addNamespace;
                        break;
                    case Collector.ObjectsIDs.ObjectControlleraddObjectNode:
                        m = addObjectNode;
                        break;
                    case Collector.ObjectsIDs.ObjectControllergetObjectRootNode:
                        m = getObjectRootNode;
                        break;
                    case Collector.ObjectsIDs.ObjectControllergetToken:
                        m = getToken;
                        break;
                    default:
                        m = ControllerMethodStandard;
                        break;
                }
                createMethod(Controller, addNamespaceMethod, (PropertyState)input, (PropertyState)output, m);
            }
        }
        private void createCollectorObject()
        {
            throw new NotImplementedException();
        }
        private void createMethod(NodeState parent, MethodState method, PropertyState inputs, PropertyState output,GenericMethodCalledEventHandler methodToBeCalled)
        {

            // a method to start the process.
            MethodState start = new MethodState(parent);

            //start.NodeId = new NodeId(2003, NamespaceIndex);
            start.NodeId = method.NodeId;
            start.BrowseName = method.BrowseName;
            start.DisplayName = method.BrowseName.Name;
            //start.BrowseName = new QualifiedName("Start", NamespaceIndex);
            //start.DisplayName = start.BrowseName.Name;
            start.ReferenceTypeId = ReferenceTypeIds.HasComponent;
            start.UserExecutable = true;
            start.Executable = true;

            // add input arguments.
            start.InputArguments = new PropertyState<Argument[]>(start);
            start.InputArguments.NodeId = inputs.NodeId;
            start.InputArguments.BrowseName = BrowseNames.InputArguments;
            start.InputArguments.DisplayName = start.InputArguments.BrowseName.Name;
            start.InputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
            start.InputArguments.ReferenceTypeId = ReferenceTypeIds.HasProperty;
            start.InputArguments.DataType = DataTypeIds.Argument;
            start.InputArguments.ValueRank = ValueRanks.OneDimension;

            List<Argument> args = new List<Argument>();
            Argument arg;
            foreach (ExtensionObject ext in (ExtensionObject[])inputs.Value)
            {
                arg = (Argument)ext.Body;
                args.Add(arg);
            }
            start.InputArguments.Value = args.ToArray();

            // add output arguments.
            start.OutputArguments = new PropertyState<Argument[]>(start);
            start.OutputArguments.NodeId = output.NodeId;
            start.OutputArguments.BrowseName = BrowseNames.OutputArguments;
            start.OutputArguments.DisplayName = start.OutputArguments.BrowseName.Name;
            start.OutputArguments.TypeDefinitionId = VariableTypeIds.PropertyType;
            start.OutputArguments.ReferenceTypeId = ReferenceTypeIds.HasProperty;
            start.OutputArguments.DataType = DataTypeIds.Argument;
            start.OutputArguments.ValueRank = ValueRanks.OneDimension;

            args = new List<Argument>();
            foreach (ExtensionObject ext in (ExtensionObject[])inputs.Value)
            {
                arg = (Argument)ext.Body;
                args.Add(arg);
            }
            start.OutputArguments.Value = args.ToArray();

            parent.AddChild(start);
            AddPredefinedNode(SystemContext, parent);
            // save in dictionary. 
            //AddPredefinedNode(SystemContext, process);

            // set up method handlers. 
            start.OnCallMethod = new GenericMethodCalledEventHandler(addNamespace);
        }
        #endregion

        #region Controller Methods
        //Fügt einen vom Client übergebene Namespace zum Server hinzu
        public ServiceResult ControllerMethodStandard(ISystemContext context, MethodState method, IList<object> inputsArguments, IList<object> outputArguments)
        {
            //System.Windows.Forms.MessageBox.Show("test");

            return StatusCodes.BadNotImplemented;
        }
        public ServiceResult addNamespace(ISystemContext context, MethodState method, IList<object> inputsArguments, IList<object> outputArguments)
        {
            Debug.WriteLine("called: " + method.DisplayName.Text + " ; addNamespace");
            if (inputsArguments[0].ToString() == null || inputsArguments[0].ToString() == "")
                return StatusCodes.BadArgumentsMissing;
            ushort index = SystemContext.NamespaceUris.GetIndexOrAppend(inputsArguments[0].ToString());
            context.SessionId.ToString();
            outputArguments[0] = index.ToString();
            return StatusCodes.Good;
            //context.SessionId;
        }

        public ServiceResult addObjectNode(ISystemContext context, MethodState method, IList<object> inputsArguments, IList<object> outputArguments)
        {

            Debug.WriteLine("called: " + method.DisplayName.Text + " ; addObjectNode");
            return StatusCodes.BadNotImplemented;
        }
        public ServiceResult getToken(ISystemContext context, MethodState method, IList<object> inputsArguments, IList<object> outputArguments)
        {

            Debug.WriteLine("called: " + method.DisplayName.Text + " ; getToken");
            return StatusCodes.BadNotImplemented;
        }
        public ServiceResult getObjectRootNode(ISystemContext context, MethodState method, IList<object> inputsArguments, IList<object> outputArguments)
        {
            context.SessionId
            Debug.WriteLine("called: " + method.DisplayName.Text + " ; getObjectRootNode");
            return StatusCodes.BadNotImplemented;
        }


        #endregion
        /// <summary>
        /// Import NodeSets from xml
        /// </summary>
        /// <param name="path">String to path of XML</param>
        private NodeStateCollection ImportXml(IDictionary<NodeId, IList<IReference>> externalReferences, string resourcepath)
        {
            NodeState mnode = null;
            NodeStateCollection predefinedNodes = new NodeStateCollection();
            try
            {
                Stream stream = new FileStream(resourcepath, FileMode.Open);
                Opc.Ua.Export.UANodeSet nodeSet = Opc.Ua.Export.UANodeSet.Read(stream);

                foreach (string namespaceUri in nodeSet.NamespaceUris)
                {
                    SystemContext.NamespaceUris.GetIndexOrAppend(namespaceUri);
                }
                nodeSet.Import(SystemContext, predefinedNodes);

                for (int ii = 0; ii < predefinedNodes.Count; ii++)
                {
                    // die gefundenen Knoten in Hauptspeicher-Struktur laden
                    AddPredefinedNode(SystemContext, predefinedNodes[ii]);
                }

                // ensure the reverse refernces exist.
                AddReverseReferences(externalReferences);

            }
            catch (Exception fehler)
            {
                Console.WriteLine("Fehler in ImportXML: --> \n" +fehler.Message);
                throw;
            }
            return predefinedNodes;
        }
      
        protected override NodeHandle GetManagerHandle(ServerSystemContext context, NodeId nodeId, IDictionary<NodeId, NodeState> cache)
        {
            lock (Lock)
            {
                // quickly exclude nodes that are not in the namespace. 
                if (!IsNodeIdInNamespace(nodeId))
                {
                    return null;
                }

                NodeState node = null;

                if (!PredefinedNodes.TryGetValue(nodeId, out node))
                {
                    return null;
                }

                NodeHandle handle = new NodeHandle();

                handle.NodeId = nodeId;
                handle.Node = node;
                handle.Validated = true;

                return handle;
            }
        }

        
        /// <summary>
        /// Verifies that the specified node exists.
        /// </summary>
        protected override NodeState ValidateNode(
            ServerSystemContext context,
            NodeHandle handle,
            IDictionary<NodeId, NodeState> cache)
        {
            // not valid if no root.
            if (handle == null)
            {
                return null;
            }

            // check if previously validated.
            if (handle.Validated)
            {
                return handle.Node;
            }

            // TBD

            return null;
        }

        #endregion
        #region Private Fields

        #endregion
    }
}