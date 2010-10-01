﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team (for details please see \doc\copyright.txt)
// This code is distributed under the GNU LGPL (for details please see \doc\license.txt)

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;

using ICSharpCode.Reports.Core.BaseClasses.Printing;
using ICSharpCode.Reports.Core.Interfaces;

namespace ICSharpCode.Reports.Core.Exporter
{
	/// <summary>
	/// Description of RowConverter.
	/// </summary>
	/// 
	
	public class GroupedRowConverter:BaseConverter
	{

		private BaseReportItem parent;
		
		public GroupedRowConverter(IDataNavigator dataNavigator,
		                    ExporterPage singlePage, ILayouter layouter):base(dataNavigator,singlePage,layouter)
		{               
		}
		
		public override ExporterCollection Convert(BaseReportItem parent, BaseReportItem item)
		{
			if (parent == null) {
				throw new ArgumentNullException("parent");
			}
			if (item == null) {
				throw new ArgumentNullException("item");
			}
			
			ISimpleContainer simpleContainer = item as ISimpleContainer;
			this.parent = parent;
			
			simpleContainer.Parent = parent;
			
			PrintHelper.AdjustParent(parent,simpleContainer.Items);
			if (PrintHelper.IsTextOnlyRow(simpleContainer)) {
				ExporterCollection myList = new ExporterCollection();

				BaseConverter.BaseConvert (myList,simpleContainer,parent.Location.X,
				                  new Point(base.SectionBounds.DetailStart.X,base.SectionBounds.DetailStart.Y));
				
				return myList;
			} else {
				return this.ConvertDataRow(simpleContainer);
			}
		}
		
		
		private ExporterCollection ConvertDataRow (ISimpleContainer simpleContainer)
		{
			ExporterCollection exporterCollection = new ExporterCollection();
			Point currentPosition = new Point(base.SectionBounds.DetailStart.X,base.SectionBounds.DetailStart.Y);
			BaseSection section = parent as BaseSection;
			
			int defaultLeftPos = parent.Location.X;

			Rectangle pageBreakRect = Rectangle.Empty;
				
			do {
				
				PrintHelper.AdjustSectionLocation (section);
				section.Size = this.SectionBounds.DetailSectionRectangle.Size;
				base.SaveSize(section.Items[0].Size);
				
				// did we have GroupedItems at all
				if (section.Items.IsGrouped) {
					
					// GetType child navigator
					IDataNavigator childNavigator = base.DataNavigator.GetChildNavigator();
					
					base.Evaluator.SinglePage.IDataNavigator = childNavigator;
					// Convert Grouping Header
					
					currentPosition = ConvertGroupHeader(exporterCollection,section,defaultLeftPos,currentPosition);
					
					childNavigator.Reset();
					childNavigator.MoveNext();
					
					//Convert children
					if (childNavigator != null) {	
						StandardPrinter.AdjustBackColor(simpleContainer,GlobalValues.DefaultBackColor);
					
						do {
							childNavigator.Fill(simpleContainer.Items);
							
							currentPosition = ConvertGroupChilds (exporterCollection,section,
							                                      simpleContainer,defaultLeftPos,currentPosition);
							pageBreakRect = PrintHelper.CalculatePageBreakRectangle((BaseReportItem)section.Items[1],currentPosition);
							
							if (PrintHelper.IsPageFull(pageBreakRect,base.SectionBounds )) {
								currentPosition = ForcePageBreak (exporterCollection,section);
//								base.BuildNewPage(exporterCollection,section);
//								currentPosition = CalculateStartPosition ();
							}
							
							
						}
						while ( childNavigator.MoveNext());
						
					
						if (PageBreakAfterGroupChange(section) ) {
							
							if ( base.DataNavigator.HasMoreData)
							{
								currentPosition = ForcePageBreak (exporterCollection,section);
								//currentPosition = CalculateStartPosition ();

							}
						}
						
						base.Evaluator.SinglePage.IDataNavigator = base.DataNavigator;
					}
				}
				else
				{
					// No Grouping at all
					currentPosition = ConvertStandardRow (exporterCollection,section,simpleContainer,defaultLeftPos,currentPosition);
				}
				
				pageBreakRect = PrintHelper.CalculatePageBreakRectangle((BaseReportItem)section.Items[0],currentPosition);
				if (PrintHelper.IsPageFull(pageBreakRect,base.SectionBounds)) {
					currentPosition= ForcePageBreak (exporterCollection,section);
//					currentPosition = CalculateStartPosition();
				}
				
				ShouldDrawBorder (section,exporterCollection);
				
				
			}
			while (base.DataNavigator.MoveNext());
			
			SectionBounds.ReportFooterRectangle =  new Rectangle(SectionBounds.ReportFooterRectangle.Left,
			                                                     section.Location.Y + section.Size.Height,
			                                                     SectionBounds.ReportFooterRectangle.Width,
			                                                     SectionBounds.ReportFooterRectangle.Height);
			return exporterCollection;
		}
		
		
		private Point ForcePageBreak(ExporterCollection exporterCollection, BaseSection section)
		{
			base.BuildNewPage(exporterCollection,section);
			return CalculateStartPosition();
		}
		
		
		private static bool PageBreakAfterGroupChange(BaseSection section)
		{
			var groupedRowCollection  = new Collection<BaseGroupedRow>(section.Items.OfType<BaseGroupedRow>().ToList());
			if (groupedRowCollection.Count > 0) {
				var groupedRow = groupedRowCollection[0];
				return groupedRow.PageBreakOnGroupChange;
			}
			return false;
		}
		
		
		private Point CalculateStartPosition()
		{
			return new Point(base.SectionBounds.PageHeaderRectangle.X,base.SectionBounds.PageHeaderRectangle.Y);
		}
		
		
		private Point ConvertGroupHeader(ExporterCollection exportList,BaseSection section,int leftPos,Point offset)
		{
			var retVal = Point.Empty;
			ReportItemCollection groupCollection = null;
			var groupedRow  = new Collection<BaseGroupedRow>(section.Items.OfType<BaseGroupedRow>().ToList());
			if (groupedRow.Count == 0) {
				groupCollection = section.Items.ExtractGroupedColumns();
				base.DataNavigator.Fill(groupCollection);
				base.FireSectionRendering(section);
				ExporterCollection list = StandardPrinter.ConvertPlainCollection(groupCollection,offset);
				
				StandardPrinter.EvaluateRow(base.Evaluator,list);
				
				exportList.AddRange(list);
				AfterConverting (section,list);
				retVal =  new Point (leftPos,offset.Y + groupCollection[0].Size.Height + 20  + (3 *GlobalValues.GapBetweenContainer));
			} else {
				retVal = ConvertStandardRow(exportList,section,groupedRow[0],leftPos,offset);
			}
			return retVal;
		}
		
		
		private Point ConvertGroupChilds(ExporterCollection mylist, BaseSection section, ISimpleContainer simpleContainer, int defaultLeftPos, Point currentPosition)
		{
			PrepareContainerForConverting(section,simpleContainer);
			Point curPos  = BaseConverter.BaseConvert(mylist,simpleContainer,defaultLeftPos,currentPosition);
			AfterConverting (section,mylist);
			return curPos;
		}
		
		
		private  Point ConvertStandardRow(ExporterCollection mylist, BaseSection section, ISimpleContainer simpleContainer, int defaultLeftPos, Point currentPosition)
		{
			base.FillRow(simpleContainer);
			PrepareContainerForConverting(section,simpleContainer);
			Point curPos = BaseConverter.BaseConvert(mylist,simpleContainer,defaultLeftPos,currentPosition);
			StandardPrinter.EvaluateRow(base.Evaluator,mylist);
			AfterConverting (section,mylist);
			return curPos;
		}
		
		
		private void AfterConverting (BaseSection section,ExporterCollection mylist)
		{
			StandardPrinter.EvaluateRow(base.Evaluator,mylist);
			section.Items[0].Size = base.RestoreSize;
			section.SectionOffset += section.Size.Height + 3 * GlobalValues.GapBetweenContainer;
		}
		
		
		private static void ShouldDrawBorder (BaseSection section,ExporterCollection list)
		{
			if (section.DrawBorder == true) {
				BaseRectangleItem br = BasePager.CreateDebugItem (section);
				BaseExportColumn bec = br.CreateExportColumn();
				bec.StyleDecorator.Location = section.Location;
				list.Insert(0,bec);
			}
		}
	}
}
