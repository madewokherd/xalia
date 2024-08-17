namespace Xalia.Viewer
{
    partial class UiDomViewer
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.element_tree = new System.Windows.Forms.TreeView();
            this.split_container = new System.Windows.Forms.SplitContainer();
            this.info_pages = new System.Windows.Forms.TabControl();
            this.properties_page = new System.Windows.Forms.TabPage();
            this.properties_view = new System.Windows.Forms.DataGridView();
            this.property_column = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.value_column = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.action_column = new System.Windows.Forms.DataGridViewButtonColumn();
            ((System.ComponentModel.ISupportInitialize)(this.split_container)).BeginInit();
            this.split_container.Panel1.SuspendLayout();
            this.split_container.Panel2.SuspendLayout();
            this.split_container.SuspendLayout();
            this.info_pages.SuspendLayout();
            this.properties_page.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.properties_view)).BeginInit();
            this.SuspendLayout();
            // 
            // element_tree
            // 
            this.element_tree.AccessibleName = "Element Tree";
            this.element_tree.Dock = System.Windows.Forms.DockStyle.Fill;
            this.element_tree.HideSelection = false;
            this.element_tree.Location = new System.Drawing.Point(0, 0);
            this.element_tree.Name = "element_tree";
            this.element_tree.Size = new System.Drawing.Size(346, 827);
            this.element_tree.TabIndex = 0;
            this.element_tree.AfterSelect += new System.Windows.Forms.TreeViewEventHandler(this.element_tree_AfterSelect);
            // 
            // split_container
            // 
            this.split_container.Dock = System.Windows.Forms.DockStyle.Fill;
            this.split_container.Location = new System.Drawing.Point(0, 0);
            this.split_container.Name = "split_container";
            // 
            // split_container.Panel1
            // 
            this.split_container.Panel1.Controls.Add(this.element_tree);
            // 
            // split_container.Panel2
            // 
            this.split_container.Panel2.Controls.Add(this.info_pages);
            this.split_container.Size = new System.Drawing.Size(1040, 827);
            this.split_container.SplitterDistance = 346;
            this.split_container.TabIndex = 1;
            // 
            // info_pages
            // 
            this.info_pages.Controls.Add(this.properties_page);
            this.info_pages.Dock = System.Windows.Forms.DockStyle.Fill;
            this.info_pages.Location = new System.Drawing.Point(0, 0);
            this.info_pages.Name = "info_pages";
            this.info_pages.SelectedIndex = 0;
            this.info_pages.Size = new System.Drawing.Size(690, 827);
            this.info_pages.TabIndex = 0;
            // 
            // properties_page
            // 
            this.properties_page.Controls.Add(this.properties_view);
            this.properties_page.Location = new System.Drawing.Point(8, 39);
            this.properties_page.Name = "properties_page";
            this.properties_page.Padding = new System.Windows.Forms.Padding(3);
            this.properties_page.Size = new System.Drawing.Size(674, 780);
            this.properties_page.TabIndex = 0;
            this.properties_page.Text = "Properties";
            this.properties_page.UseVisualStyleBackColor = true;
            // 
            // properties_view
            // 
            this.properties_view.AllowUserToAddRows = false;
            this.properties_view.AllowUserToDeleteRows = false;
            this.properties_view.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.properties_view.AutoSizeRowsMode = System.Windows.Forms.DataGridViewAutoSizeRowsMode.DisplayedCells;
            this.properties_view.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.properties_view.ColumnHeadersVisible = false;
            this.properties_view.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.property_column,
            this.value_column,
            this.action_column});
            this.properties_view.Dock = System.Windows.Forms.DockStyle.Fill;
            this.properties_view.Location = new System.Drawing.Point(3, 3);
            this.properties_view.Name = "properties_view";
            this.properties_view.RowHeadersVisible = false;
            this.properties_view.RowHeadersWidth = 82;
            this.properties_view.RowTemplate.Height = 33;
            this.properties_view.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.properties_view.Size = new System.Drawing.Size(668, 774);
            this.properties_view.TabIndex = 0;
            this.properties_view.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.properties_view_CellClick);
            // 
            // property_column
            // 
            this.property_column.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.DisplayedCells;
            this.property_column.HeaderText = "Property";
            this.property_column.MinimumWidth = 10;
            this.property_column.Name = "property_column";
            this.property_column.ReadOnly = true;
            this.property_column.Width = 10;
            // 
            // value_column
            // 
            this.value_column.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.value_column.HeaderText = "Value";
            this.value_column.MinimumWidth = 10;
            this.value_column.Name = "value_column";
            // 
            // action_column
            // 
            this.action_column.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.AllCellsExceptHeader;
            this.action_column.HeaderText = "Action";
            this.action_column.MinimumWidth = 10;
            this.action_column.Name = "action_column";
            this.action_column.Width = 10;
            // 
            // UiDomViewer
            // 
            this.AccessibleName = "Xalia Ui DOM Viewer";
            this.AutoScaleDimensions = new System.Drawing.SizeF(12F, 25F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1040, 827);
            this.Controls.Add(this.split_container);
            this.Name = "UiDomViewer";
            this.Text = "UiDomViewer";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.UiDomViewer_FormClosed);
            this.split_container.Panel1.ResumeLayout(false);
            this.split_container.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.split_container)).EndInit();
            this.split_container.ResumeLayout(false);
            this.info_pages.ResumeLayout(false);
            this.properties_page.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.properties_view)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TreeView element_tree;
        private System.Windows.Forms.SplitContainer split_container;
        private System.Windows.Forms.TabControl info_pages;
        private System.Windows.Forms.TabPage properties_page;
        private System.Windows.Forms.DataGridView properties_view;
        private System.Windows.Forms.DataGridViewTextBoxColumn property_column;
        private System.Windows.Forms.DataGridViewTextBoxColumn value_column;
        private System.Windows.Forms.DataGridViewButtonColumn action_column;
    }
}