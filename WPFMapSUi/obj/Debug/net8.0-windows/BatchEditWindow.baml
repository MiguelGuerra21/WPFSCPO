   M S B A M L   `   `   `  ����D  @WPFMapSUi, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null%clr-namespace:WPFMapSUi	WPFMapSUi  5   	       WPFMapSUi.BatchEditWindow   5   	   W SPresentationCore, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35e aSystem.Windows.Controls.Ribbon, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089R NWindowsBase, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35F 9http://schemas.microsoft.com/winfx/2006/xaml/presentation     5   	   R NSystem.Xaml, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     5   	   @d2http://schemas.microsoft.com/expression/blend/2008     5   	   Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     5   	   )localclr-namespace:WPFMapSUi     5   	     � Title$  Edición múltiple��5   	   $	��500��6"   $	��800��6/    � WindowStartupLocation$ CenterOwner=�5	   	   .��5
      �  � RowDefinitions 5   
   �� 5      $
��Auto �6   �� 5      $��* �6   �� 5      $
��Auto �6   .+�5   
   �� $��0��6   $��5q�6"   .+�5      �� $&�� Edición de múltiples elementos��6   $
��Bold"�6>   �� 5      -   5         ��Name$ selectedCountText��5      ��63   �� *SelectedFeaturesCount+�� 5      5      &   System.Windows.Controls.DataGrid  5   
   -   5          Name$ batchAttributeGrid��5      $��1��6/   $��5q�6<      AutoGenerateColumns$ False��5         CanUserAddRows$ False��5         CanUserDeleteRows$ False��5         HeadersVisibility$ Column=�5      	   SelectionUnit$	 CellOrRowHeader=�5      
   RowHeaderWidth$
 0Q�5         SelectionMode$ Single=�5       $   HorizontalScrollBarVisibility$
 Auto=�5!         Columns 5"      0  *System.Windows.Controls.DataGridTextColumn  5#      ,  &System.Windows.Controls.DataGridColumn   Header$ Atributo��6%      
IsReadOnly$
 True��6O      Width5  /System.Windows.Controls.DataGridLengthConverter$ * 6a   1  +System.Windows.Controls.DataGridBoundColumn   Binding 67   �� *Key+6a     5$      $ Valor actual��6%   $
 True��6\   $ * 6n    6;   �� *CurrentValue+6n     5%      $ Nuevo valor��6%   $ * 6d    6:   �� *
NewValue+ �� Mode$ TwoWay=�6d   5'      �� 5*   
   $��2��6    �� Orientation$ 
Horizontal=�6"   $��Right=�6;   $��5q�6W   .+�5+      �� -   5        $��Cancelar��5+      $��80��6(   $��5q�63   6>   �� 5,      -   5        $��Aplicar cambios��5,      $	��120��6/   $��5q�6;   6F   5-      5.      5/      