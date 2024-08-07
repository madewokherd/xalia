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
            ((System.ComponentModel.ISupportInitialize)(this.split_container)).BeginInit();
            this.split_container.Panel1.SuspendLayout();
            this.split_container.SuspendLayout();
            this.SuspendLayout();
            // 
            // element_tree
            // 
            this.element_tree.AccessibleName = "Element Tree";
            this.element_tree.Dock = System.Windows.Forms.DockStyle.Fill;
            this.element_tree.Location = new System.Drawing.Point(0, 0);
            this.element_tree.Name = "element_tree";
            this.element_tree.Size = new System.Drawing.Size(346, 827);
            this.element_tree.TabIndex = 0;
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
            this.split_container.Size = new System.Drawing.Size(1040, 827);
            this.split_container.SplitterDistance = 346;
            this.split_container.TabIndex = 1;
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
            this.split_container.Panel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.split_container)).EndInit();
            this.split_container.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TreeView element_tree;
        private System.Windows.Forms.SplitContainer split_container;
    }
}